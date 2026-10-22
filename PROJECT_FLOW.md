# StudyApp — Entire Project Flow

How the whole system fits together, from a click in the UI to an AI answer.

---

## 1. The big picture

```mermaid
flowchart LR
    subgraph CLIENT["Angular front end — localhost:4200"]
        UI["Workspace UI<br/>folders, notes, documents,<br/>scribbles, chat"]
    end

    subgraph API["StudyApp.Api — HTTP layer"]
        CTRL["8 controllers<br/>api/workspaces, folders, notes,<br/>documents, scribbles, chat,<br/>ai, indexing"]
    end

    subgraph APP["StudyApp.Application — business rules"]
        SRV["Workspace, Folder, Note,<br/>Document, Scribble services"]
        CHAT["ChatService<br/>RetrievalService<br/>SystemPromptBuilder"]
        SCOPE["ScopeResolver<br/>(who may see what)"]
    end

    subgraph INFRA["StudyApp.Infrastructure — plumbing"]
        REPO["Repositories<br/>EF Core"]
        WORKER["EventProcessingWorker<br/>(background)"]
        EXTRACT["Extractors, DocumentChunker,<br/>ImageCaptioner"]
        AI["AI providers<br/>Ollama, OpenAI-compatible"]
    end

    subgraph STORES["Stores and models"]
        PG[("Postgres + pgvector<br/>localhost:5433")]
        FILES[("Local files<br/>StudyApp.Api/files")]
        LLM["Ollama or OpenAI-compatible<br/>chat / embedding / vision model"]
    end

    UI -->|"JSON over HTTP<br/>CORS: localhost:4200"| CTRL
    CTRL --> SRV
    CTRL --> CHAT
    CHAT --> SCOPE
    SRV --> REPO
    CHAT --> REPO
    SCOPE --> REPO
    SRV -->|"publish domain event"| WORKER
    WORKER --> EXTRACT
    WORKER --> AI
    WORKER --> REPO
    EXTRACT --> FILES
    CHAT --> AI
    AI --> LLM
    REPO --> PG
    WORKER --> PG
```

**Rules of the layers (enforced by project references):**

```mermaid
flowchart TD
    API["StudyApp.Api"] --> APP["StudyApp.Application"]
    API --> INFRA["StudyApp.Infrastructure"]
    INFRA --> APP
    APP --> DOMAIN["StudyApp.Domain"]
    INFRA --> DOMAIN
    TESTS["StudyApp.Tests"] --> APP
```

Application never references Infrastructure. That is why it only talks through interfaces
(`IVectorChunkRepository`, `IEmbeddingService`, `IFileStorage`).

---

## 2. Flow A — content comes in (indexing)

Every time you upload a document, save a note, or draw on a scribble, the same background
pipeline runs. The HTTP request returns **immediately**; indexing happens afterwards.

```mermaid
flowchart TD
    START(["User uploads a document,<br/>saves a note, or autosaves a scribble"])

    START --> UPLOAD["Service saves the content"]
    UPLOAD --> STORE["Local files<br/>files/documents/{id}_{name}"]
    UPLOAD --> ROW["Database row<br/>IndexStatus = Pending"]

    ROW --> EVENT["Publish DomainEvent<br/>DocumentCreated / NoteUpdated / ScribbleUpdated<br/>ScopeId + ScopeType + SourceId"]
    EVENT --> CHAN[["Bounded channel<br/>10,000 events, drop-oldest"]]

    CHAN --> W1["EventProcessingWorker<br/>(one event at a time)"]

    W1 --> DEL{"Delete event?"}
    DEL -->|"yes"| CLEAN["Delete its chunks<br/>IndexStatus = Pending<br/>done"]
    DEL -->|"no"| FP["1. Fingerprint the source"]

    FP --> SAME{"Same fingerprint AND<br/>same pipeline version?"}
    SAME -->|"yes"| SKIP["Skip — no re-embedding"]
    SAME -->|"no"| MARK["IndexStatus = Extracting"]

    MARK --> X1{"Source type?"}

    X1 -->|"document"| XD["Detect file type by magic bytes<br/>PDF, DOCX, PPTX, XLSX, MD, TXT, image"]
    X1 -->|"scribble"| XS["Parse the Excalidraw scene JSON<br/>typed text + frames + pasted images"]
    X1 -->|"note"| XN["Title + body"]

    XD --> CAP["Images bigger than 12 KB go to the<br/>vision model: transcribe text,<br/>describe the diagram"]
    XS --> CAP
    CAP --> BLOCKS["List of blocks:<br/>Text, Heading, Table, Code,<br/>Image, Caption"]

    XN --> BLOCKS
    BLOCKS --> CHUNK["DocumentChunker — about 500 tokens each<br/>headings are boundaries<br/>tables, code, figures stay whole"]
    CHUNK --> HASH["Drop duplicate chunks<br/>(same SHA-256 text)"]
    HASH --> EMB["Embedding model turns each chunk<br/>into 768 numbers"]
    EMB --> IMG["Image bytes are written to<br/>files/images/{sourceType}/..."]
    IMG --> SAVE["Atomic replace:<br/>delete old chunks, insert new ones<br/>IndexStatus = Indexed, chunk count, page count"]

    SAVE --> READY(["Searchable by the chat"])

    style CHAN fill:#e8f0ff
    style SAVE fill:#d6f5d6
    style SKIP fill:#fff3cd
```

**Why the fingerprint matters:** a scribble autosaves constantly. Without it, every keystroke
would re-run the vision model. It now hashes the *extracted* content, so moving a shape costs
nothing, while editing text does trigger a re-index.

---

## 3. Flow B — a question goes out (chat)

```mermaid
sequenceDiagram
    autonumber
    participant U as User (Angular)
    participant C as ChatController
    participant CS as ChatService
    participant SR as ScopeResolver
    participant RS as RetrievalService
    participant DB as Postgres + pgvector
    participant PB as SystemPromptBuilder
    participant LLM as Chat model
    participant M as ChatMessages

    U->>C: POST api/chat/sessions/{id}/messages
    C->>CS: SendMessageAsync
    CS->>M: save the user message
    CS->>DB: load the session + its scope targets
    CS->>SR: ResolveAsync(targets)
    SR->>DB: expand workspace → folders,<br/>note ↔ scribble links, ancestors
    SR-->>CS: RetrievalFilter (allowed IDs only)

    alt filter is empty
        CS--xC: 409 ScopeUnavailable
    end

    CS->>LLM: embed the question (768 numbers)
    CS->>RS: RetrieveAsync(question, embedding, filter)
    RS->>DB: vector search (cosine distance)
    RS->>DB: keyword search (Postgres full text)
    Note over RS: 1. drop hits below MinScore<br/>2. merge both lists (RRF)<br/>3. cap per source, cap total<br/>4. token budget
    RS-->>CS: chunks + labelled sources (S1, S2 …)

    CS->>PB: BuildAsync(scope, retrieval)
    PB->>DB: AI settings, workspace, folder ancestors
    PB-->>CS: system prompt =<br/>guardrails + workspace context +<br/>[S1] document "name" · p.42 + text

    CS->>LLM: send prompt + last 12 messages
    LLM-->>CS: answer
    CS->>M: save answer + chunk IDs + SourcesJson
    CS-->>C: message DTO with sources
    C-->>U: answer + clickable sources
```

If the model is unreachable the answer is **not** lost — the service stores a plain
"(Assistant unavailable: …)" message instead of throwing, so the chat history stays consistent.

---

## 4. Flow C — scope: what a chat is allowed to see

A chat session can point at several things at once, and each target can be widened.

```mermaid
flowchart TD
    T(["Chat targets<br/>(ChatSessionScope rows)"])

    T --> W["Workspace target"]
    W --> W1["Workspace id<br/>+ every folder inside it"]

    T --> F["Folder target"]
    F --> F1["That folder + all sub-folders"]
    F1 --> F2["optional: ancestors<br/>walk up to the workspace"]

    T --> D["Document target"]
    D --> D1["Just that document"]
    D1 --> D2["optional: ancestors<br/>everything in its folder"]

    T --> N["Note target"]
    N --> N1["Just that note"]
    N1 --> N2["optional: linked scribbles<br/>(NoteScribbleLink)"]
    N1 --> N3["optional: ancestors"]

    T --> S["Scribble target"]
    S --> S1["Just that scribble"]
    S1 --> S2["optional: linked notes"]

    W1 --> FILTER[["RetrievalFilter<br/>WorkspaceIds, FolderIds,<br/>DocumentIds, NoteIds, ScribbleIds"]]
    F2 --> FILTER
    D2 --> FILTER
    N2 --> FILTER
    N3 --> FILTER
    S2 --> FILTER

    FILTER --> Q["Every query is OR-ed across these ids<br/>nothing outside them is ever returned"]
    Q --> EMPTY{"Empty?"}
    EMPTY -->|"yes"| ERR["409 — the scope died,<br/>never silently widen"]
    EMPTY -->|"no"| OK["Search"]

    style ERR fill:#ffd6d6
    style OK fill:#d6f5d6
```

`GET api/chat/sessions/{id}/scope` returns how many chunks the scope currently resolves to,
which is the fastest way to debug "why does the chat know nothing".

---

## 5. Data model

```mermaid
erDiagram
    WORKSPACE ||--o{ FOLDER : "contains"
    FOLDER ||--o{ FOLDER : "parent of"
    WORKSPACE ||--o{ NOTE : "contains"
    WORKSPACE ||--o{ DOCUMENT : "contains"
    WORKSPACE ||--o{ SCRIBBLE : "contains"
    FOLDER ||--o{ NOTE : "contains"
    FOLDER ||--o{ DOCUMENT : "contains"
    FOLDER ||--o{ SCRIBBLE : "contains"

    NOTE ||--o{ NOTE_SCRIBBLE_LINK : "linked"
    SCRIBBLE ||--o{ NOTE_SCRIBBLE_LINK : "linked"

    NOTE ||--o{ VECTOR_CHUNK : "indexed into"
    DOCUMENT ||--o{ VECTOR_CHUNK : "indexed into"
    SCRIBBLE ||--o{ VECTOR_CHUNK : "indexed into"

    CHAT_SESSION ||--o{ CHAT_SESSION_SCOPE : "targets"
    CHAT_SESSION ||--o{ CHAT_MESSAGE : "messages"

    AI_SETTINGS {
        string ChatProvider
        string ChatBaseUrl
        string ChatModel
        string EmbeddingModel
        int EmbeddingDimensions
        string VisionModel
        string Guardrails
        string SystemPromptTemplate
    }

    VECTOR_CHUNK {
        guid SourceId
        string SourceType
        guid ScopeId
        string ChunkText
        string HeadingPath
        string Locator
        string Kind
        string ImagePath
        string ContentHash
        int PipelineVersion
        vector Embedding
    }

    CHAT_MESSAGE {
        string Role
        string Content
        string SourcesJson
    }
```

**One row per chunk, always carrying its own scope** (`ScopeId` + `ScopeType`). That is what
makes filtering cheap: the chat never has to join back to folders to know what is allowed.

`AI_SETTINGS` is a single row. A row saved through the API always beats the `Ai` section in
`appsettings.json`, which only seeds the first read.

---

## 6. API surface

| Area | Routes |
| --- | --- |
| Workspaces | `GET/POST api/workspaces`, `GET/PUT/DELETE api/workspaces/{id}` |
| Folders | `GET/POST api/folders`, `GET/PUT/DELETE api/folders/{id}`, `GET api/folders/{id}/descendants` |
| Notes | `GET/POST api/notes`, `GET/PUT/DELETE api/notes/{id}` |
| Documents | `POST api/documents/upload`, `GET api/documents`, `GET api/documents/{id}/content`, `DELETE api/documents/{id}` |
| Scribbles | `GET/POST api/scribbles`, `GET/PUT/DELETE api/scribbles/{id}`, `POST/DELETE api/scribbles/{id}/link/{noteId}` |
| Chat | `POST api/chat/sessions`, `GET api/chat/sessions`, `GET api/chat/sessions/{id}/scope`, `GET/POST api/chat/sessions/{id}/messages` |
| AI settings | `GET/PUT api/ai/settings`, `POST api/ai/test` |
| Indexing | `POST api/indexing/reindex/{sourceType}/{sourceId}`, `POST api/indexing/reindex` |

Every response is wrapped in the same envelope by `ApiControllerBase` (`ApiResponse<T>`:
success flag, code, message, data). Validation failures return 400 with a field-by-field
dictionary instead of a raw model-state dump.

---

## 7. Where everything lives

```mermaid
flowchart LR
    subgraph L1["StudyApp.Api"]
        A1["Controllers/"]
        A2["Program.cs — DI, CORS, OpenTelemetry"]
        A3["files/ — uploaded documents and extracted images"]
    end

    subgraph L2["StudyApp.Application"]
        B1["Workspaces, Folders, Notes,<br/>Documents, Scribbles — services + DTOs"]
        B2["Chat/ — ChatService, prompt builder"]
        B3["Retrieval/ — options + hybrid search"]
        B4["Scoping/ — ScopeResolver, RetrievalFilter"]
        B5["Embeddings/ — pipeline version, block model"]
        B6["Repositories/ — interfaces only"]
    end

    subgraph L3["StudyApp.Domain"]
        C1["Entities/ — Workspace, Folder, Note,<br/>Document, Scribble, VectorChunk,<br/>ChatSession, ChatMessage, AiSettings"]
        C2["Enums/"]
    end

    subgraph L4["StudyApp.Infrastructure"]
        D1["Repositories/ — EF Core implementations"]
        D2["Embeddings/ — TextExtractor, DocumentChunker,<br/>readers/ (PDF, OpenXml, Spreadsheet,<br/>Markdown, ScribbleCanvas)"]
        D3["Ai/ — Ollama + OpenAI-compatible<br/>chat, embedding, vision"]
        D4["Events/ — channel publisher + worker"]
        D5["Migrations/"]
    end

    L1 --> L2
    L1 --> L4
    L4 --> L2
    L2 --> L3
    L4 --> L3
```

---

## 8. Two things this diagram does *not* show yet

1. **No authentication or authorisation.** Every route is open. `Workspaces.OwnerUserId`
   exists but is never read. There is no user identity anywhere in the flow, so "scope"
   currently means "what this chat points at", not "what this person is allowed to see".
2. **No Mermaid module.** The planned diagram feature (turning study material into Mermaid
   charts) is not in the pipeline yet; it would plug in as a separate step after retrieval.
