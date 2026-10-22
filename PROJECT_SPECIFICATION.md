# StudyApp API — Backend Specification for the Angular Frontend

> **Audience:** Angular frontend development agent building the StudyApp client.
> **Purpose:** Single source of truth for every API contract, data model, AI configuration feature, and business flow the frontend must match.
> **Backend stack:** .NET 10 Web API · PostgreSQL 17 + pgvector · EF Core · Ollama / OpenAI-compatible LLM & embedding providers.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Global API Conventions](#2-global-api-conventions)
3. [API Endpoint Reference](#3-api-endpoint-reference)
4. [AI Settings Configuration](#4-ai-settings-configuration)
5. [Data Models & Entities](#5-data-models--entities)
6. [Business Logic Flows](#6-business-logic-flows)
7. [Frontend Integration Guide](#7-frontend-integration-guide)
8. [Configuration & Environment](#8-configuration--environment)
9. [Known MVP Limitations](#9-known-mvp-limitations)

---

## 1. Project Overview

StudyApp is a personal study application. Users organize study material inside **Workspaces** (optionally split into nested **Folders**), create **Notes**, draw **Scribbles** (Excalidraw-style canvases), upload **Documents**, and chat with an AI assistant that answers using Retrieval-Augmented Generation (RAG) over that material.

The AI layer is fully **configurable at runtime from the frontend**: provider, base URL, model, and API keys (BYOK — Bring Your Own Key) for both chat and embeddings, plus global **guardrails** and a **system prompt template**.

### 1.1 Solution Structure (4 layers)

| Project | Layer | Contains | Rules |
|---|---|---|---|
| `StudyApp.Domain` | Domain | Entities (`Workspace`, `Folder`, `Note`, `Scribble`, `Document`, `ChatSession`, `ChatMessage`, `VectorChunk`, `AiSettings`, `NoteScribbleLink`) and enums (`ScopeType`, `ChatRole`, `AiProviderType`) | No dependencies on other layers |
| `StudyApp.Application` | Application | DTOs (records), service interfaces + implementations, repository interfaces, event abstractions, AI abstractions (`IAiSettingsService`, `IChatProvider`, `IEmbeddingProvider`, `AiDefaults`), `SystemPromptBuilder`, `ChatService` | Depends only on Domain |
| `StudyApp.Infrastructure` | Infrastructure | EF Core `AppDbContext` + migrations, repository implementations, AI provider implementations (Ollama, OpenAI-compatible), file storage, text extraction/chunking, event channel + background worker | Depends on Domain + Application |
| `StudyApp.Api` | API / Presentation | Controllers, `Program.cs` (DI wiring), `appsettings.json` | Depends on Application + Infrastructure |

### 1.2 Tech Stack

- **.NET 10** (`net10.0`), ASP.NET Core Web API, controllers pattern
- **PostgreSQL 17** with the **pgvector** extension (`vector(768)` columns for embeddings)
- **EF Core** with `Npgsql` + `Pgvector.EntityFrameworkCore`
- **AI providers** (swappable at runtime): `Ollama` and `OpenAiCompatible`
- **OpenAPI + Scalar** API reference UI in Development (`/scalar/v1`)
- **OpenTelemetry → SigNoz** (traces, metrics, logs via OTLP; exports fail softly if absent)
- **Docker Compose** for local deployment

### 1.3 Architecture Pattern

The backend follows a strict layered pattern:

```
Controller → Service (Application) → Repository interface (Application) → Repository impl (Infrastructure) → AppDbContext
```

- All endpoints return the **`ApiResponse<T>` envelope** (see §2.2) except the raw document-content download.
- Cross-cutting work (text extraction → chunking → embedding) is **decoupled via a DomainEvent queue** processed by a background worker (§6.5). Services never call the embedding pipeline directly.

### 1.4 Base URLs

| Endpoint | URL |
|---|---|
| API (local dev, `dotnet run`) | `http://localhost:5031` |
| API (https profile) | `https://localhost:7237` / `http://localhost:5031` |
| API (Docker Compose) | `http://localhost:5031` |
| Scalar API docs (Development only) | `http://localhost:5031/scalar/v1` |
| OpenAPI JSON (Development only) | `http://localhost:5031/openapi/v1.json` |
| PostgreSQL (host port) | `localhost:5433` |
| Ollama (host) | `http://localhost:11434` |

---

## 2. Global API Conventions

### 2.1 Response Envelope

Every JSON endpoint returns `ApiResponse<T>`:

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "data": { },
  "message": null,
  "errors": null
}
```

| Field | Type | Notes |
|---|---|---|
| `isSuccess` | `boolean` | `true` for 2xx |
| `statusCode` | `number` | Mirrors the HTTP status code |
| `data` | `T \| null` | Payload; `null` for error responses |
| `message` | `string \| null` | Error message (e.g. `"Note not found"`) |
| `errors` | `{ [field: string]: string[] } \| null` | Validation errors only |

**Important exceptions:**

- **HTTP 204 responses have an empty body** — no envelope at all. Do not attempt to parse JSON for them.
- **`GET /api/documents/{id}/content`** returns the raw file stream (e.g. `application/pdf`), not the envelope.
- **500 responses in non-Development environments** return `application/problem+json`: `{ "title": "An unexpected error occurred.", "status": 500 }` — not the envelope.
- The .NET `Guid` route parameter casing is normalized by ASP.NET (`:guid` / `:Guid` constraints are equivalent).

### 2.2 JSON Serialization Rules

- **Property names:** camelCase (ASP.NET Core default for `System.Text.Json`).
- **Enums are serialized as strings**, because `JsonStringEnumConverter` is registered globally. Use the exact PascalCase enum values:

| Enum | Serialized values |
|---|---|
| `ScopeType` | `"Workspace"`, `"Folder"` |
| `ChatRole` | `"User"`, `"Assistant"` (internal only) |
| `AiProviderType` | `"Ollama"`, `"OpenAiCompatible"` |

- **Exception:** `ChatMessageDto.role` is a **lowercase** string — `"user"` / `"assistant"` — deliberately converted by the service layer.
- **Dates:** `DateTime` values are UTC, serialized as ISO-8601 with `Z` suffix (e.g. `"2026-10-15T12:34:56.789Z"`). Use Angular's `Date` parsing / `date` pipe accordingly.
- **IDs:** all identifiers are GUIDs (UUID v4 format strings).

### 2.3 Validation Error Shape (HTTP 400)

Invalid model state returns:

```json
{
  "isSuccess": false,
  "statusCode": 400,
  "data": null,
  "message": "Validation failed",
  "errors": {
    "request.Title": ["The Title field is required."]
  }
}
```

### 2.4 Error Status Summary

| Status | Meaning | Body |
|---|---|---|
| `200` | Success | Envelope with `data` |
| `201` | Created (POST workspace / folder / note / scribble / chat session / document upload) | Envelope with `data` + `Location` header pointing at the GET-by-id route |
| `204` | Success, no content (all PUTs, DELETEs, link/unlink operations) | Empty |
| `400` | Validation failure, or AI settings business-rule failure (`Fail(400, message)`) | Envelope |
| `404` | Entity not found | Envelope with `message` |
| `500` | Unhandled exception / FK constraint violation (e.g. deleting a folder that has children) | `application/problem+json` in production, developer exception page in Development |

### 2.5 CORS

The API registers a policy named `AllowAngular` that allows **only** `http://localhost:4200` with any header and any method. The Angular dev server must run on port **4200**, otherwise requests will be blocked by the browser.

### 2.6 The Scope Concept (Critical for the Frontend)

Notes, Scribbles, Documents, and ChatSessions are all attached to a **Scope**, defined by the pair `(scopeId, scopeType)`:

- `scopeType: "Workspace"` → `scopeId` is a `Workspace.Id`. The scope covers the whole workspace **including every folder inside it**.
- `scopeType: "Folder"` → `scopeId` is a `Folder.Id`. The scope covers that folder **and all of its descendant folders**.

Folders are the only hierarchical entity (`Folder.ParentFolderId` — `null` means root folder of a workspace). Chats inside a folder can still resolve their parent workspace for workspace-level context (see §6.4).

---

## 3. API Endpoint Reference

### 3.1 AiSettingsController — `/api/ai`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| GET | `/api/ai/settings` | — | `200` + `AiSettingsDto` | — |
| PUT | `/api/ai/settings` | JSON `UpdateAiSettingsRequest` | `200` + `AiSettingsDto` | `400` envelope (missing base URL/model, wrong embedding dimensions) |
| POST | `/api/ai/test` | JSON `TestAiRequest` (all fields optional) | `200` + `AiTestReport` | — (connection failures are reported *inside* the 200 body) |

**GET `/api/ai/settings` response example** (API keys are masked, never returned raw):

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "data": {
    "chatProvider": "Ollama",
    "chatBaseUrl": "http://localhost:11434",
    "chatModel": "gemma4:31b-cloud",
    "chatApiKeySet": true,
    "chatApiKeyPreview": "••••abcd",
    "guardrails": "You are a study assistant. Use the following retrieved notes/documents...",
    "systemPromptTemplate": "{guardrails}\n\nWorkspace context:\n{workspace_context}\n\nRetrieved context:\n{retrieved_context}",
    "embeddingProvider": "Ollama",
    "embeddingBaseUrl": "http://localhost:11434",
    "embeddingModel": "nomic-embed-text",
    "embeddingApiKeySet": false,
    "embeddingApiKeyPreview": null,
    "embeddingDimensions": 768,
    "updatedAt": "2026-10-15T09:00:00Z"
  },
  "message": null,
  "errors": null
}
```

**PUT `/api/ai/settings` request** — `ChatProvider`, `ChatBaseUrl`, `ChatModel`, `EmbeddingProvider`, `EmbeddingBaseUrl`, `EmbeddingModel` are **required**; everything else optional:

```json
{
  "chatProvider": "OpenAiCompatible",
  "chatBaseUrl": "https://api.groq.com/openai",
  "chatModel": "llama-3.3-70b-versatile",
  "chatApiKey": "gsk_...",
  "embeddingProvider": "Ollama",
  "embeddingBaseUrl": "http://localhost:11434",
  "embeddingModel": "nomic-embed-text",
  "embeddingApiKey": null,
  "embeddingDimensions": null,
  "guardrails": null,
  "systemPromptTemplate": null
}
```

**POST `/api/ai/test`** — empty object `{}` tests the *saved* settings; supplying fields tests *unsaved form values* (field-by-field merge with saved settings; `null` = use saved value):

```json
{
  "chatProvider": "OpenAiCompatible",
  "chatBaseUrl": "https://api.groq.com/openai",
  "chatApiKey": "gsk_...",
  "chatModel": "llama-3.3-70b-versatile",
  "embeddingProvider": null,
  "embeddingBaseUrl": null,
  "embeddingApiKey": null,
  "embeddingModel": null
}
```

**POST `/api/ai/test` response example:**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "data": {
    "chat": {
      "success": true,
      "message": "Connected. Model 'llama-3.3-70b-versatile' is available.",
      "models": ["llama-3.3-70b-versatile", "mixtral-8x7b-32768"],
      "modelAvailable": true
    },
    "embeddings": {
      "success": true,
      "message": "Connected. 'nomic-embed-text' returned 768 dimensions.",
      "dimensions": 768,
      "dimensionsMatchSchema": true
    }
  },
  "message": null,
  "errors": null
}
```

Full semantics of these three endpoints are in [§4](#4-ai-settings-configuration).

### 3.2 ChatController — `/api/chat`

| Method | Route | Request | Success |
|---|---|---|---|
| POST | `/api/chat/sessions` | JSON `CreateChatSessionRequest` | `201` + `ChatSessionDto` |
| GET | `/api/chat/sessions?scopeId={guid}&scopeType={ScopeType}` | Query params | `200` + `ChatSessionDto[]` (newest first) |
| GET | `/api/chat/sessions/{id}/messages` | — | `200` + `ChatMessageDto[]` (chronological, last 50) |
| POST | `/api/chat/sessions/{id}/messages` | JSON `SendMessageRequest` | `200` + `ChatMessageDto` (the assistant reply) |

**CreateChatSessionRequest JSON:**

```json
{
  "scopeId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeType": "Workspace",
  "title": "Kubernetes basics"
}
```

**SendMessageRequest JSON:**

```json
{ "content": "Explain what a ReplicaSet does" }
```

**ChatMessageDto JSON (both user and assistant messages):**

```json
{
  "id": "31cc0bca-0f6b-4172-8096-5e7177a1d3c3",
  "role": "assistant",
  "content": "A ReplicaSet ensures a specified number of pod replicas...",
  "retrievedChunkIds": ["1f4eea90-d45c-4fec-a38b-5d96c78f5df2"],
  "createdAt": "2026-10-15T12:34:56.789Z"
}
```

Key behaviors (details in [§6.3](#63-rag-chat-flow-step-by-step)):

- `POST .../messages` is **synchronous** — it blocks until the LLM replies (server HTTP client timeout is 5 minutes for chat calls). Set a generous Angular HTTP timeout for this call (e.g. 120–300 s).
- `role` is strictly lowercase: `"user"` or `"assistant"`.
- `retrievedChunkIds` is populated only on assistant messages — the vector chunks that were fed to the LLM (source traceability).
- If the LLM is unreachable, the endpoint still returns `200`; the assistant message content is `"(Assistant unavailable: <provider error>)"`. Display it like any assistant message; do **not** treat it as a client error.
- Posting to a non-existent session id → `500` (`InvalidOperationException: Chat session not found`). Always create/fetch the session first.
- There are **no** rename/delete endpoints for chat sessions or messages in the current version.

### 3.3 DocumentsController — `/api/documents`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| POST | `/api/documents/upload` | `multipart/form-data` | `201` + `DocumentDto` | — |
| GET | `/api/documents?scopeId={guid}&scopeType={ScopeType}` | Query params | `200` + `DocumentDto[]` | — |
| GET | `/api/documents/{id}/content` | — | `200` + raw file stream with correct `Content-Type` and `Content-Disposition` | `404` envelope |
| DELETE | `/api/documents/{id}` | — | `204` (empty) | `404` envelope |

**Upload request — multipart form fields:**

| Field | Type | Notes |
|---|---|---|
| `scopeId` | `Guid` (text) | Workspace or Folder id |
| `scopeType` | `string` | `"Workspace"` or `"Folder"` |
| `file` | binary | The document file |

Angular example:

```ts
const form = new FormData();
form.append('scopeId', scopeId);
form.append('scopeType', 'Workspace');
form.append('file', file, file.name);
this.http.post<ApiResponse<DocumentDto>>(`${base}/api/documents/upload`, form);
```

**DocumentDto JSON:**

```json
{
  "id": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeId": "1f4eea90-d45c-4fec-a38b-5d96c78f5df2",
  "scopeType": "Workspace",
  "filename": "Kubernetes_book.pdf",
  "createdAt": "2026-10-15T08:00:00Z"
}
```

Content-Type mapping for `GET /{id}/content`: `.pdf` → `application/pdf`, `.png` → `image/png`, `.jpg`/`.jpeg` → `image/jpeg`, `.txt`/`.md` → `text/plain`, `.docx` / `.xlsx` → their OOXML types, anything else → `application/octet-stream`.

> Note: deletion removes the file from disk (`files/documents/`) **and** queues vector-chunk cleanup. Deletion is permanent — confirm in the UI.

### 3.4 FoldersController — `/api/folders`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| POST | `/api/folders` | JSON `CreateFolderRequest` | `201` + `FolderDto` (+ `Location` header) | — |
| GET | `/api/folders/{id}` | — | `200` + `FolderDto` | `404` envelope |
| GET | `/api/folders?workspaceId={guid}&parentFolderId={guid?}` | Query params (`parentFolderId` optional) | `200` + `FolderDto[]` (ordered by name) | — |
| GET | `/api/folders/{id}/descendants` | — | `200` + `Guid[]` (**includes the folder itself** + all descendants) | — |
| PUT | `/api/folders/{id}` | JSON `UpdateFolderRequest` | `204` (empty) | `404` envelope |
| DELETE | `/api/folders/{id}` | — | `204` (empty) | `404` envelope |

**CreateFolderRequest / UpdateFolderRequest JSON:**

```json
// POST — parentFolderId = null creates a root folder
{ "workspaceId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871", "parentFolderId": null, "name": "Chapter 1" }

// PUT — only the name is updatable
{ "name": "Chapter 1 — Basics" }
```

**FolderDto JSON:**

```json
{
  "id": "206a0334-4fde-4188-8a59-94da21c71fad",
  "workspaceId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "parentFolderId": null,
  "name": "Chapter 1",
  "createdAt": "2026-10-14T10:00:00Z"
}
```

**Folder-tree usage for the frontend:**

- Build the tree by calling `GET /api/folders?workspaceId={id}` **without** `parentFolderId` to get root folders, or with `parentFolderId={folderId}` to lazy-load children of a node.
- `GET /api/folders/{id}/descendants` returns a flat list of GUIDs (self included) — convenient to know the full folder subtree when building a folder picker.
- A folder cannot be moved to another parent or workspace (PUT only changes `Name`).
- **Deleting a folder that contains subfolders fails** (DB FK is `Restrict`, surfaces as 500). Delete children first, deepest-first.
- Deleting a folder does **not** delete its notes/scribbles/documents/chat sessions. They remain stored with a dangling `scopeId`; the UI should delete or move them before removing the folder.

### 3.5 NotesController — `/api/notes`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| POST | `/api/notes` | JSON `CreateNoteRequest` | `201` + `NoteDto` (+ `Location` header) | — |
| GET | `/api/notes/{id}` | — | `200` + `NoteDto` | `404` envelope |
| GET | `/api/notes?scopeId={guid}&scopeType={ScopeType}` | Query params | `200` + `NoteDto[]` | — |
| PUT | `/api/notes/{id}` | JSON `UpdateNoteRequest` | `204` (empty) | `404` envelope |
| DELETE | `/api/notes/{id}` | — | `204` (empty) | `404` envelope |

**Create / Update JSON:**

```json
// POST
{ "scopeId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871", "scopeType": "Workspace", "title": "K8s basics", "content": "Pods are..." }

// PUT — full replace of title + content (no merge semantics here)
{ "title": "K8s basics v2", "content": "Updated body..." }
```

**NoteDto JSON:**

```json
{
  "id": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeType": "Workspace",
  "title": "K8s basics",
  "content": "Pods are...",
  "createdAt": "2026-10-15T08:00:00Z",
  "updatedAt": "2026-10-15T09:30:00Z"
}
```

> Create/Update/Delete each publish a domain event that re-indexes (or cleans) the note's vector chunks in the background ([§6.2](#62-content-ingestion--vectorization-pipeline)). `updatedAt` is set server-side on every PUT.

### 3.6 ScribblesController — `/api/scribbles`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| POST | `/api/scribbles` | JSON `CreateScribbleRequest` | `201` + `ScribbleDto` (+ `Location` header) | — |
| GET | `/api/scribbles/{id}` | — | `200` + `ScribbleDto` | `404` envelope |
| GET | `/api/scribbles?scopeId={guid}&scopeType={ScopeType}` | Query params | `200` + `ScribbleDto[]` | — |
| PUT | `/api/scribbles/{id}` | JSON `UpdateScribbleRequest` | `204` (empty) | `404` envelope |
| DELETE | `/api/scribbles/{id}` | — | `204` (empty) | `404` envelope |
| POST | `/api/scribbles/{id}/link/{noteId}` | — | `204` (empty) | — |
| DELETE | `/api/scribbles/{id}/link/{noteId}` | — | `204` (empty) | — |

**Create / Update JSON** — `canvasData` is the **raw Excalidraw scene serialized as a JSON string** (stored in a `jsonb` column):

```json
{
  "scopeId": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeType": "Folder",
  "title": "ReplicaSet diagram",
  "canvasData": "{\"type\":\"excalidraw\",\"elements\":[...],\"appState\":{...}}"
}
```

> `canvasData` starts as `"{}"` if not provided. After a PUT, the frontend should send the whole canvas again (full replace). Updating a scribble **clears its `extractedText`** server-side to avoid stale embeddings.

**ScribbleDto JSON:**

```json
{
  "id": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "scopeId": "206a0334-4fde-4188-8a59-94da21c71fad",
  "scopeType": "Folder",
  "title": "ReplicaSet diagram",
  "canvasData": "{...}",
  "extractedText": null,
  "createdAt": "2026-10-15T08:00:00Z",
  "updatedAt": null
}
```

**Link / Unlink semantics:**

- `POST /api/scribbles/{scribbleId}/link/{noteId}` creates a `NoteScribbleLink` row (composite PK `NoteId + ScribbleId`).
- `DELETE` removes it. Both are **idempotent-friendly** only in the happy path: linking the same pair twice will hit a PK violation → 500. Guard the UI (disable "link" when already linked, or check existing links).
- There is no API to list links for a note/scribble in the current version — track links client-side after load, or extend the backend.
- `extractedText` is `null` today (reserved for a future OCR/vision step). Scribbles without `extractedText` are **skipped** by the embedding pipeline.

### 3.7 WorkspacesController — `/api/workspaces`

| Method | Route | Request | Success | Failure |
|---|---|---|---|---|
| POST | `/api/workspaces` | JSON `CreateWorkspaceRequest` | `201` + `WorkSpaceDto` (+ `Location` header) | — |
| GET | `/api/workspaces` | — | `200` + `WorkSpaceDto[]` | — |
| GET | `/api/workspaces/{id}` | — | `200` + `WorkSpaceDto` | `404` envelope |
| PUT | `/api/workspaces/{id}` | JSON `UpdateWorkspaceRequest` | `204` (empty) | `404` envelope |
| DELETE | `/api/workspaces/{id}` | — | `204` (empty) | `404` envelope |

**Create / Update JSON:**

```json
{
  "name": "Kubernetes",
  "description": "CKA exam prep — core Kubernetes concepts",
  "customInstructions": "Always explain with kubectl examples. Use CKA exam phrasing."
}
```

**WorkSpaceDto JSON:**

```json
{
  "id": "6c6ca2b6-3bd8-4a5a-828c-89d1834e6871",
  "name": "Kubernetes",
  "description": "CKA exam prep — core Kubernetes concepts",
  "customInstructions": "Always explain with kubectl examples. Use CKA exam phrasing.",
  "createdAt": "2026-10-13T07:00:00Z"
}
```

Field rules (enforced by the service):

| Field | Limit | Create | Update semantics |
|---|---|---|---|
| `name` | 200 chars, required | required | always replaced (send current value) |
| `description` | 2000 chars | optional | `null` = keep, `""`/whitespace = clear, value = replace |
| `customInstructions` | 4000 chars | optional | `null` = keep, `""`/whitespace = clear, value = replace |

`description` and `customInstructions` are the **workspace-level AI context** — they get pinned into the system prompt of every chat in the workspace (and every chat in any folder under it). See [§6.4](#64-system-prompt-assembly).

> Deleting a workspace does **not** cascade to folders/notes/scribbles/documents/chats (no FK cascade). The UI should offer explicit cleanup or warn the user.

---

## 4. AI Settings Configuration

This is the feature the frontend must surface as an **AI Settings page**. All of it is managed through the three `/api/ai/*` endpoints (§3.1) — there is no other configuration channel at runtime.

### 4.1 The `AiSettings` Entity — Database Schema

Single-row table `AiSettings` (PostgreSQL). The whole app reads one settings record; it is **created on first save** (before that, defaults from `appsettings.json`'s `Ai` section are served).

| Column | CLR type → PG type | Constraints | Meaning |
|---|---|---|---|
| `Id` | `Guid` → `uuid` | PK | Row id (single row) |
| `ChatProvider` | `string` (enum-as-text) | not null | `"Ollama"` or `"OpenAiCompatible"` |
| `ChatBaseUrl` | `string` → `varchar(500)` | not null | Root URL, no trailing slash (server trims) |
| `ChatModel` | `string` → `varchar(200)` | not null | e.g. `llama-3.3-70b-versatile` |
| `ChatApiKey` | `string?` → `varchar(500)` | nullable | BYOK key; **never returned raw** |
| `Guardrails` | `string?` → `text` | nullable | Global rules pinned into every system prompt; `null` = built-in default |
| `SystemPromptTemplate` | `string?` → `text` | nullable | Template with placeholders `{guardrails}`, `{workspace_context}`, `{retrieved_context}` |
| `EmbeddingProvider` | `string` (enum-as-text) | not null | `"Ollama"` or `"OpenAiCompatible"` |
| `EmbeddingBaseUrl` | `string` → `varchar(500)` | not null | Root URL |
| `EmbeddingModel` | `string` → `varchar(200)` | not null | Must produce **768-dim** vectors |
| `EmbeddingApiKey` | `string?` → `varchar(500)` | nullable | BYOK key; **never returned raw** |
| `EmbeddingDimensions` | `int` → `integer` | not null, default `768` | Must equal 768 — enforced by service validation |
| `UpdatedAt` | `DateTime` → `timestamptz` | not null | Set server-side on every save |

Constant: `AiSettings.EmbeddingDimension = 768` — matches the `vector(768)` column on `VectorChunk.Embedding`.

### 4.2 Defaults & Runtime Precedence

1. **`appsettings.json` → `"Ai"` section** seeds `AiDefaults` (chat/embedding base URLs + models). `Program.cs` binds it once at startup.
2. **First read** (`GET /api/ai/settings` or the first chat/embedding call) with an empty DB → an in-memory `AiSettings` is built from those defaults. Nothing is persisted yet.
3. **First save** (`PUT /api/ai/settings`) → the row is inserted. **From then on the DB row always wins**; appsettings changes have no effect until the DB row is deleted.
4. Settings are read **fresh on every chat message and every embedding call** (`ConfiguredChatLlmService`, `ConfiguredEmbeddingService`, `SystemPromptBuilder`, `EventProcessingWorker` via `IEmbeddingService`). Editing settings in the UI **takes effect immediately — no restart**.

Built-in default guardrails (when `Guardrails` is null/empty):

```
You are a study assistant. Use the following retrieved notes/documents to answer the user's question.
If the context doesn't contain the answer, say so rather than guessing.
```

Built-in default system prompt template (when `SystemPromptTemplate` is null/empty):

```
{guardrails}

Workspace context:
{workspace_context}

Retrieved context:
{retrieved_context}
```

### 4.3 Reading Settings — What the Frontend Receives

The `AiSettingsDto` **never exposes raw API keys**. Instead:

- `chatApiKeySet` / `embeddingApiKeySet` (`boolean`) — whether a key is stored.
- `chatApiKeyPreview` / `embeddingApiKeyPreview` (`string | null`) — masked value: `"••••"` for keys ≤ 4 chars, otherwise `"••••" + last 4 chars` (e.g. `••••x9f2`).
- `guardrails` and `systemPromptTemplate` are returned **fully resolved**: if the DB value is null, the response contains the built-in defaults (so the UI can display editable text that matches what the AI actually uses).

Settings-page UX contract:

- Show key state via `*ApiKeySet` and the masked preview. Keep the key input **empty** by default with placeholder like "Leave blank to keep current key".
- The form should show the resolved `guardrails`/`systemPromptTemplate` values; a "Reset to default" button can send `""` to clear them back to built-ins.

### 4.4 Updating Settings — Merge Semantics (Critical)

`PUT /api/ai/settings` follows this convention (documented in the DTO itself):

| Field | `null` | `""` (empty) | Any value |
|---|---|---|---|
| `chatApiKey`, `embeddingApiKey` | keep stored key | **clear** stored key | replace with trimmed new key |
| `guardrails`, `systemPromptTemplate` | keep stored value | **reset to built-in default** | replace with trimmed value |

Validation rules (violations → `400` envelope with `message`):

1. `chatBaseUrl` and `chatModel` must be non-blank → otherwise `"Chat base URL and chat model are required"`.
2. `embeddingBaseUrl` and `embeddingModel` must be non-blank → otherwise `"Embedding base URL and embedding model are required"`.
3. If `embeddingDimensions` is supplied and ≠ `768` → error explaining the vector column is sized `vector(768)` and requires an EF migration to change.
4. Base URLs are normalized server-side: trailing `/` is trimmed.

`embeddingDimensions` defaults to `768` when omitted (`null`).

**Angular form-mapping recipe:**

```ts
// Build the PUT body from the form:
{
  chatProvider: form.chatProvider,                  // "Ollama" | "OpenAiCompatible"
  chatBaseUrl: form.chatBaseUrl,
  chatModel: form.chatModel,
  chatApiKey: form.chatApiKey === '' ? null : form.chatApiKey,   // null keeps stored key
  guardrails: form.guardrailsTouched ? form.guardrails : null,   // null keeps stored value
  systemPromptTemplate: form.templateTouched ? form.systemPromptTemplate : null,
  embeddingProvider: form.embeddingProvider,
  embeddingBaseUrl: form.embeddingBaseUrl,
  embeddingModel: form.embeddingModel,
  embeddingApiKey: form.embeddingApiKey === '' ? null : form.embeddingApiKey,
  embeddingDimensions: null                                      // always null = 768
}
```

> To *clear* a key, send exactly `""` (not `null`). To *keep* it, send `null` (or omit).

### 4.5 Supported AI Providers

Both chat and embedding are configured **independently** — you can chat via OpenAI-compatible (e.g. Groq/OpenRouter/OpenAI) while embedding locally via Ollama, or vice versa.

| Provider value | Used for | Chat call | Model listing | Auth |
|---|---|---|---|---|
| `"Ollama"` | chat + embeddings | `POST {baseUrl}/api/chat` (`model`, `messages`, `stream: false`) | `GET {baseUrl}/api/tags` | Optional `Authorization: Bearer <apiKey>` if a key is set |
| `"OpenAiCompatible"` | chat + embeddings | `POST {baseUrl}/v1/chat/completions` | `GET {baseUrl}/v1/models` | `Authorization: Bearer <apiKey>` when set |

`baseUrl` is the **root** of the provider (the suffix paths are appended by the backend). Examples:

- Ollama local: `http://localhost:11434`
- Ollama in Docker on host: `http://host.docker.internal:11434`
- OpenAI: `https://api.openai.com`
- Groq: `https://api.groq.com/openai`
- OpenRouter: `https://openrouter.ai/api`

Defaults (from `appsettings.json` / `AiDefaults`):

- Chat provider `Ollama`, base `http://localhost:11434`, model `gemma4:31b-cloud`
- Embedding provider `Ollama`, base `http://localhost:11434`, model `nomic-embed-text` (768 dims)

### 4.6 Guardrails & System Prompt Template

- **Guardrails** are global behavioral rules stored in `AiSettings.Guardrails`, substituted into `{guardrails}` of the template, and therefore pinned into **every** chat system prompt, regardless of workspace.
- **SystemPromptTemplate** controls the overall prompt skeleton via three placeholders:

| Placeholder | Replaced with |
|---|---|
| `{guardrails}` | Settings guardrails, or built-in default |
| `{workspace_context}` | Workspace name + description + custom instructions (see §6.4) |
| `{retrieved_context}` | Top-K retrieved vector chunks joined by `\n\n---\n\n`, or `"(no relevant context found)"` |

- The frontend settings page should:
  - Let the user edit both as free text (multi-line).
  - Validate that custom templates contains all three placeholders (soft warning; the backend performs a plain string `Replace`, so missing placeholders simply drop that context).
  - Offer a "restore defaults" action that sends `""` for either field.
  - Note: substitutions are **plain string replacement** — if the user writes `{guardrails}` inside their guardrails text, it will not be re-expanded.

### 4.7 Embedding Dimensions Constraint

The `VectorChunk.Embedding` column is `vector(768)`. Consequences:

- Any embedding model that does **not** return exactly 768 floats cannot be used. Both the settings validation (`POST /api/ai/test` and the PUT save) and the runtime embedding path (`ConfiguredEmbeddingService`) enforce this and throw descriptive `InvalidOperationException`s.
- Known good models: `nomic-embed-text` (768). Known bad examples for this schema: OpenAI `text-embedding-3-small` (1536), `text-embedding-ada-002` (1536).
- The settings UI should surface `dimensionsMatchSchema` from the test response prominently, and disable "Save" (or warn hard) when embeddings test returns `dimensionsMatchSchema: false`.

### 4.8 Connection Testing — `POST /api/ai/test`

Purpose: let the settings page verify a configuration **before** saving. Request fields are merged with the saved settings field-by-field (`null` = use saved value), so the UI can send only the currently edited fields — or the full form — to test the exact configuration the user sees.

Response contract (`AiTestReport`):

| Field | Meaning |
|---|---|
| `chat.success` | HTTP call to the chat provider succeeded |
| `chat.message` | Human-readable status, e.g. `"Connected. Model 'X' is available."` / `"Connected, but 'X' was not found. Available: ..."` / error text |
| `chat.models` | All model names the provider listed (up to all, UI can truncate; message lists first 10 on mismatch) |
| `chat.modelAvailable` | `true` when the configured model exists in the listing (tolerant of `name` vs `name:latest`), or when the provider returned zero models (empty list → `false` for availability but `success: true`) |
| `embeddings.success` | The provider returned an embedding for a probe string |
| `embeddings.message` | Status incl. actual returned dimensions when mismatched |
| `embeddings.dimensions` | Actual dimensions returned (null on failure) |
| `embeddings.dimensionsMatchSchema` | `dimensions == 768` |

**Never** are failures returned as HTTP errors from this endpoint — always `200` with the report inside. The UI renders per-row pass/fail states.

Suggested UI flow: "Test connection" button → `POST /api/ai/test` with current (possibly unsaved) form values → show two result cards (Chat / Embeddings) with status, message, and a model dropdown populated from `chat.models` when the model isn't found.

### 4.9 What Happens to AI Settings at Runtime

- Editing chat settings affects the **next chat message** — nothing is cached beyond a per-request scope.
- Editing embedding settings affects the **next embedding job** (background worker per event) and the next chat message embedding (query embedding uses the *current* embedding model; chunks already indexed were embedded with whatever model was configured when they were ingested — changing embedding models does **not** re-index existing content; re-index by re-saving the source note/scribble/document).
- Deleting the `AiSettings` row in the DB (manual SQL) reverts the app to the appsettings defaults.

---

## 5. Data Models & Entities

### 5.1 Relationship Overview

```
Workspace 1 ── * Folder (Folder.ParentFolderId → Folder, self-referencing tree, no FK to Workspace)
Workspace / Folder  ←── ScopeId + ScopeType ──  Note, Scribble, Document, ChatSession
ChatSession 1 ── * ChatMessage (ChatMessage.RetrievedChunkIds: uuid[] — source traceability)
NoteScribbleLink (NoteId, ScribbleId) — composite-key join table, sole coupling between Note and Scribble
VectorChunk — derived index rows: SourceId/SourceType point at a Note, Scribble or Document; ScopeId/ScopeType copy the source's scope
AiSettings — single-row global configuration table
```

Notes: entities attach to a scope via `(ScopeId, ScopeType)` — **not** via FK navigation properties. There are no cascades from Workspace/Folder to content; see §9 for implications.

### 5.2 Workspace

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `name` | string | no | max 200, required | |
| `description` | string | yes | max 2000 | Fed into system prompt workspace context |
| `customInstructions` | string | yes | max 4000 | Pinned first-context for every chat in this workspace |
| `createdAt` | DateTime (UTC) | no | | |

### 5.3 Folder

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `workspaceId` | Guid | no | | Logical owner workspace (no FK) |
| `parentFolderId` | Guid? | yes | FK → Folder, `Restrict` delete | `null` = root folder |
| `name` | string | no | max 200, required | |
| `createdAt` | DateTime (UTC) | no | | |

Sibling folders are returned ordered by `name`. Recursive SQL (CTE) powers descendants/scope expansion.

### 5.4 Note

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `scopeId` | Guid | no | indexed with ScopeType | |
| `scopeType` | `ScopeType` | no | stored as text | `"Workspace"` / `"Folder"` |
| `title` | string | no | max 300, required | |
| `content` | string | no | default `""` | Plain text; becomes embedding source |
| `createdAt` | DateTime (UTC) | no | | |
| `updatedAt` | DateTime? (UTC) | yes | | Set server-side on PUT |

### 5.5 Scribble

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `scopeId` / `scopeType` | Guid / `ScopeType` | no | stored as text, indexed | |
| `title` | string | no | max 300, required | |
| `canvasData` | string | no | `jsonb` column, default `"{}"` | Raw Excalidraw scene JSON, string-serialized |
| `extractedText` | string? | yes | | Reserved for OCR/vision step; `null` today → scribbles are not embedded yet. Cleared on every update |
| `createdAt` / `updatedAt` | DateTime / DateTime? | no / yes | | |

### 5.6 NoteScribbleLink

| Property | Type | Notes |
|---|---|---|
| `noteId` | Guid | part of composite PK |
| `scribbleId` | Guid | part of composite PK |

### 5.7 Document

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `scopeId` / `scopeType` | Guid / `ScopeType` | no | stored as text, indexed | |
| `filename` | string | no | max 300, required | Original filename |
| `storagePath` | string | no | | Relative path under `files/documents/` — not exposed via API |
| `createdAt` | DateTime (UTC) | no | | |

Files on disk are named `{documentId}_{filename}` under the content-root `files/documents/` (Docker: bind-mounted volume).

### 5.8 ChatSession

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `scopeId` / `scopeType` | Guid / `ScopeType` | no | stored as text, indexed | |
| `title` | string | no | default `"New chat"` | Client-supplied at creation |
| `createdAt` | DateTime (UTC) | no | | Sessions listed newest-first |

### 5.9 ChatMessage

| Property | Type | Nullable | Constraints | Notes |
|---|---|---|---|---|
| `id` | Guid | no | PK | |
| `chatSessionId` | Guid | no | indexed | |
| `role` | `ChatRole` | no | stored as text | `"User"` / `"Assistant"` in DB; DTO exposes lowercase |
| `content` | string | no | | |
| `retrievedChunkIds` | `List<Guid>` | no | `uuid[]` PG array | Chunks that grounded the assistant reply (empty on user messages) |
| `createdAt` | DateTime (UTC) | no | | |

### 5.10 VectorChunk (derived RAG index — not directly exposed via any endpoint)

| Property | Type | Notes |
|---|---|---|
| `id` | Guid | PK |
| `sourceId` | Guid | Note / Scribble / Document id |
| `sourceType` | string(20) | `"note"` \| `"scribble"` \| `"document"` (lowercase) |
| `scopeId` | Guid | copied from source |
| `scopeType` | string(20) | `"workspace"` \| `"folder"` (lowercase) |
| `chunkText` | string | chunk of extracted text |
| `embedding` | `Vector` | `vector(768)` pgvector column |
| `createdAt` | DateTime (UTC) | |

Indexes: `SourceId`; `(ScopeId, ScopeType)`. Search uses pgvector cosine distance (`<=>`).

### 5.11 AiSettings

See the full column-by-column schema in [§4.1](#41-the-aisettings-entity--database-schema).

### 5.12 Enums

| Enum | Values | Serialized as |
|---|---|---|
| `ScopeType` | `Workspace = 0`, `Folder = 1` | `"Workspace"`, `"Folder"` |
| `ChatRole` | `User = 0`, `Assistant = 1` | DB text `"User"`/`"Assistant"`; DTO lowercase `"user"`/`"assistant"` |
| `AiProviderType` | `Ollama = 0`, `OpenAiCompatible = 1` | `"Ollama"`, `"OpenAiCompatible"` |
| `DomainEventType` (internal) | `NoteCreated/Updated/Deleted`, `ScribbleCreated/Updated/Deleted`, `DocumentCreated/Updated/Deleted` | — |
| `SourceType` (internal) | `Note`, `Scribble`, `Document` | — |

---

## 6. Business Logic Flows

### 6.1 Scope Resolution

Before a chat retrieves context, the backend expands the session's scope into a **list of searchable scope ids** (`ScopeResolver`):

| Session scope | Resolved ids |
|---|---|
| `Workspace(wsId)` | `wsId` + **every folder id anywhere in that workspace** (recursive CTE over `ParentFolderId`, starting at root folders) |
| `Folder(folderId)` | `folderId` + **all descendant folder ids** (recursive CTE, includes self) |

Retrieval filters `VectorChunk.ScopeId ∈ resolvedIds`. Consequence for the UI: a chat started at the workspace level can retrieve material stored in any folder (and vice versa), but **never** material from a sibling workspace. `GET /api/folders/{id}/descendants` exposes the same descendant expansion for the frontend.

### 6.2 Content Ingestion → Vectorization Pipeline

Every Note / Scribble / Document create, update, or delete funnels into an **asynchronous, sequential, background pipeline**:

```
Note service ─┐
Scribble svc ─┼─► IEventPublisher.PublishAsync(DomainEvent)        (in-request, instant)
Document svc ─┘         │
                        ▼
        Channel<DomainEvent>  (singleton, unbounded, in-memory)
                        │
                        ▼
   EventProcessingWorker (BackgroundService, one event at a time)
                        │
        ┌───────────────┴────────────────┐
        ▼                                ▼
 Deleting event                    Create/Update event
        │                                │
 DeleteBySourceId(sourceId)      TextExtractor.ExtractAsync(sourceType, sourceId)
 (chunks removed)                        │
                                 empty? ──► skip (log + return)
                                         │
                                 DeleteBySourceId (clear stale chunks)
                                         │
                                 TextChunker.Chunk(text, 800 chars, 100 char overlap)
                                         │
                                 embed each chunk (current embedding settings, must be 768-d)
                                         │
                                 insert VectorChunk rows (SourceId, SourceType, ScopeId, ScopeType)
```

Extraction rules per source type:

| Source | Extraction | Status |
|---|---|---|
| `note` | `Note.Content` | Fully works |
| `scribble` | `Scribble.ExtractedText` | `null` today → **skipped** (future OCR/vision step). Updates clear it deliberately |
| `document` | Text read from the stored file | **Only `.txt` files** are extractable in the MVP; PDFs/images/DOCX/XLSX are stored and downloadable but **not embedded** yet |

Frontend implications:

- Ingestion is **eventually consistent**. Right after saving a note, a chat question about it may miss it (chunks not yet written). There is no API to query indexing status.
- Updating a note replaces its chunks (delete + re-embed). Deleting removes chunks.
- Events are **in-memory**: restarting the API drops queued events. If the app restarts mid-queue, re-save the source to re-index.

### 6.3 RAG Chat Flow (Step by Step)

`POST /api/chat/sessions/{id}/messages` executes synchronously (`ChatService.SendMessageAsync`):

1. **Load session** — throws `InvalidOperationException("Chat session not found")` → 500 if the id is wrong.
2. **Persist the user message** (`role = User`, `retrievedChunkIds = []`).
3. **Embed the user's text** with the *current* embedding settings (768-d enforced; failure here → 500 since it is not caught).
4. **Resolve scope ids** (§6.1).
5. **Vector search** — cosine distance (`<=>`), filtered to the resolved scope ids, **top 6 chunks** (`TopK = 6`).
6. **Load conversation history** — last **12** stored messages for the session (excluding the in-flight user message). Prompt history = at most 11 prior turns + the new user message. *No summarization — v1 truncates silently.*
7. **Build the system prompt** (§6.4).
8. **Call the configured chat provider** — via the saved settings (provider/baseUrl/model/apiKey resolved per call).
   - On `InvalidOperationException` (unreachable provider, empty response, etc.) the reply becomes `"(Assistant unavailable: <message>)"` and the request still succeeds with `200`.
9. **Persist the assistant message** with `retrievedChunkIds` = the 6 (or fewer) chunk ids used.
10. **Return the assistant `ChatMessageDto`**.

The **user message is not echoed in the POST response** — it was persisted server-side. The response contains only the assistant reply. The Angular client should optimistically render the user's message, then append the response message (or refetch history after the call).

`GET /api/chat/sessions/{id}/messages` returns the **most recent 50 messages in chronological order** (oldest → newest). Sessions themselves (`GET /api/chat/sessions?scopeId=&scopeType=`) are ordered **newest first**.

### 6.4 System Prompt Assembly

`SystemPromptBuilder.BuildAsync` composes the prompt on every message:

```
template.SystemPromptTemplate
   .Replace("{guardrails}",       settings.Guardrails ?? default)
   .Replace("{workspace_context}", BuildWorkspaceContext(workspace))
   .Replace("{retrieved_context}", chunks joined by "\n\n---\n\n" | "(no relevant context found)")
```

**Workspace context** — resolved from the session scope:

- Workspace scope → the workspace itself.
- Folder scope → **walks up the folder tree to the root folder** (safety cap 25 hops), then takes that root's `WorkspaceId`. So a chat in a deeply nested folder still receives its workspace's description and custom instructions.

The workspace context block is assembled from non-empty fields only:

```
Workspace: {name}
Description: {description}
Workspace instructions:
{customInstructions}
```

Fallback when the workspace cannot be resolved: `"(no workspace context available)"`.

**The effective context stack for any chat message is therefore:**

1. Global guardrails (AI settings)
2. Workspace name / description / custom instructions (first context)
3. Retrieved vector chunks (Top 6)
4. Capped conversation history (last ~12 messages)

### 6.5 Event System

- `DomainEvent` record: `{ EventType, SourceId, SourceType, ScopeId, ScopeType ("workspace"|"folder"), ContentRef, Timestamp }`.
- Published by `NoteService`, `ScribbleService`, `DocumentService` on every create/update/delete (Note/Scribble/Document only — folders, workspaces, chats do not emit events).
- Transport: in-process `System.Threading.Channels` unbounded channel (singleton) → `EventProcessingWorker` (`BackgroundService`) processes **one at a time**, logging failures and continuing (errors never crash the API).
- Purpose in this system: keeping `VectorChunk`s in sync with content. The frontend never interacts with it directly, but it explains ingestion latency (§6.2).

---

## 7. Frontend Integration Guide

### 7.1 Cross-Cutting Angular Setup

```
src/environments/environment.ts
  apiBaseUrl: 'http://localhost:5031'      // dev — CORS already allows http://localhost:4200
```

- Run the Angular dev server on **port 4200** (CORS allow-list).
- Create one Angular service per controller boundary, mirroring the table in §3.
- An HTTP interceptor that unwraps the `ApiResponse<T>` envelope (return `data` on `isSuccess`, throw a typed error otherwise) keeps feature code clean. Remember the 204-empty-body and raw-file exceptions.
- Set a long timeout for `POST /api/chat/sessions/{id}/messages` (LLM latency; server allows up to 5 min).

### 7.2 TypeScript Models (mirror of the DTOs)

```ts
// ---- envelope ----
export interface ApiResponse<T> {
  isSuccess: boolean;
  statusCode: number;
  data: T | null;
  message: string | null;
  errors: Record<string, string[]> | null;
}

// ---- enums (string unions — match server serialization exactly) ----
export type ScopeType = 'Workspace' | 'Folder';
export type AiProviderType = 'Ollama' | 'OpenAiCompatible';
export type ChatRole = 'user' | 'assistant';

// ---- workspaces ----
export interface WorkspaceDto {
  id: string; name: string;
  description: string | null;
  customInstructions: string | null;
  createdAt: string; // ISO-8601 UTC
}
export interface CreateWorkspaceRequest { name: string; description?: string | null; customInstructions?: string | null; }
export interface UpdateWorkspaceRequest { name: string; description?: string | null; customInstructions?: string | null; }

// ---- folders ----
export interface FolderDto { id: string; workspaceId: string; parentFolderId: string | null; name: string; createdAt: string; }
export interface CreateFolderRequest { workspaceId: string; parentFolderId: string | null; name: string; }
export interface UpdateFolderRequest { name: string; }

// ---- notes ----
export interface NoteDto { id: string; scopeId: string; scopeType: ScopeType; title: string; content: string; createdAt: string; updatedAt: string | null; }
export interface CreateNoteRequest { scopeId: string; scopeType: ScopeType; title: string; content: string; }
export interface UpdateNoteRequest { title: string; content: string; }

// ---- scribbles ----
export interface ScribbleDto { id: string; scopeId: string; scopeType: ScopeType; title: string; canvasData: string; extractedText: string | null; createdAt: string; updatedAt: string | null; }
export interface CreateScribbleRequest { scopeId: string; scopeType: ScopeType; title: string; canvasData: string; }
export interface UpdateScribbleRequest { title: string; canvasData: string; }

// ---- documents ----
export interface DocumentDto { id: string; scopeId: string; scopeType: ScopeType; filename: string; createdAt: string; }

// ---- chat ----
export interface ChatSessionDto { id: string; scopeId: string; scopeType: ScopeType; title: string; createdAt: string; }
export interface CreateChatSessionRequest { scopeId: string; scopeType: ScopeType; title: string; }
export interface ChatMessageDto { id: string; role: ChatRole; content: string; retrievedChunkIds: string[]; createdAt: string; }
export interface SendMessageRequest { content: string; }

// ---- AI settings ----
export interface AiSettingsDto {
  chatProvider: AiProviderType; chatBaseUrl: string; chatModel: string;
  chatApiKeySet: boolean; chatApiKeyPreview: string | null;
  guardrails: string; systemPromptTemplate: string;
  embeddingProvider: AiProviderType; embeddingBaseUrl: string; embeddingModel: string;
  embeddingApiKeySet: boolean; embeddingApiKeyPreview: string | null;
  embeddingDimensions: number; updatedAt: string;
}
export interface UpdateAiSettingsRequest {
  chatProvider: AiProviderType; chatBaseUrl: string; chatModel: string; chatApiKey: string | null;
  embeddingProvider: AiProviderType; embeddingBaseUrl: string; embeddingModel: string;
  embeddingApiKey: string | null; embeddingDimensions?: number | null;
  guardrails?: string | null; systemPromptTemplate?: string | null;
}
export interface TestAiRequest {
  chatProvider?: AiProviderType | null; chatBaseUrl?: string | null; chatApiKey?: string | null; chatModel?: string | null;
  embeddingProvider?: AiProviderType | null; embeddingBaseUrl?: string | null; embeddingApiKey?: string | null; embeddingModel?: string | null;
}
export interface ChatConnectionResult { success: boolean; message: string; models: string[]; modelAvailable: boolean; }
export interface EmbeddingConnectionResult { success: boolean; dimensions: number | null; dimensionsMatchSchema: boolean; message: string; }
export interface AiTestReport { chat: ChatConnectionResult; embeddings: EmbeddingConnectionResult; }
```

### 7.3 Suggested Service Methods (one per controller)

```ts
// AiSettingsService
getSettings(): Observable<ApiResponse<AiSettingsDto>>                      // GET  /api/ai/settings
updateSettings(r: UpdateAiSettingsRequest): Observable<ApiResponse<AiSettingsDto>> // PUT /api/ai/settings
testConnection(r: TestAiRequest): Observable<ApiResponse<AiTestReport>>    // POST /api/ai/test

// WorkspaceService
list(): GET /api/workspaces  · get(id): GET /api/workspaces/{id}
create(r): POST /api/workspaces  · update(id, r): PUT /api/workspaces/{id}  · delete(id): DELETE /api/workspaces/{id}

// FolderService
create(r): POST /api/folders · getById(id) · getChildren(workspaceId, parentFolderId?) · getDescendants(id) · update(id, r) · delete(id)

// NoteService / ScribbleService
create · getById · listByScope(scopeId, scopeType) · update · delete
// ScribbleService extras: link(scribbleId, noteId): POST /api/scribbles/{id}/link/{noteId}; unlink(...): DELETE ...

// DocumentService
upload(scopeId, scopeType, file: File): FormData → POST /api/documents/upload
listByScope(scopeId, scopeType) · getContentUrl(id) = `${base}/api/documents/${id}/content` · delete(id)
// For downloads/previews: fetch via HttpClient as a blob, or set the URL directly (endpoint returns the raw file).

// ChatService
createSession(r): POST /api/chat/sessions
getSessions(scopeId, scopeType): GET /api/chat/sessions?scopeId=...&scopeType=...
getHistory(sessionId): GET /api/chat/sessions/{id}/messages
sendMessage(sessionId, content): POST /api/chat/sessions/{id}/messages
```

### 7.4 Feature → Endpoint Mapping

| Frontend screen / feature | Backend endpoints |
|---|---|
| Workspace list / switcher | `GET/POST/PUT/DELETE /api/workspaces` |
| Workspace settings (name, description, AI custom instructions) | `GET /api/workspaces/{id}`, `PUT /api/workspaces/{id}` |
| Folder tree / explorer | `GET /api/folders?workspaceId=…&parentFolderId=…`, `GET /api/folders/{id}/descendants`, POST/PUT/DELETE folders |
| Note editor | `GET /api/notes?scopeId&scopeType`, POST/PUT/DELETE `/api/notes` |
| Scribble canvas (Excalidraw) | `GET /api/scribbles?scopeId&scopeType`, POST/PUT/DELETE `/api/scribbles`, link/unlink with `/api/scribbles/{id}/link/{noteId}` |
| Document library + upload + preview/download | `GET /api/documents?scopeId&scopeType`, `POST /api/documents/upload`, `GET /api/documents/{id}/content`, DELETE |
| Chat panel | `POST /api/chat/sessions`, `GET /api/chat/sessions?scopeId&scopeType`, `GET/POST /api/chat/sessions/{id}/messages` |
| AI Settings page | `GET/PUT /api/ai/settings`, `POST /api/ai/test` |

### 7.5 Frontend Behavior Checklist (gotchas)

1. **Enums are PascalCase strings** in JSON (`"Workspace"`, `"Ollama"`) — except chat message `role`, which is lowercase (`"user"`/`"assistant"`).
2. **204 = empty body.** PUT/DELETE/link/unlink succeed with no envelope. Detect success by status, not body.
3. **Chat send response = assistant message only.** Persist/refetch the user message client-side.
4. **`"(Assistant unavailable: …)"`** is a *successful* 200 assistant message — style it as a warning in the chat UI if desired, but do not treat it as a transport error.
5. **Long-running chat POST** — configure a 120–300 s timeout for that specific call.
6. **Ingestion latency** — notes/scribbles/documents are embedded asynchronously after save; only `.txt` documents and notes currently produce chunks. Don't promise "instant" AI knowledge of just-uploaded material.
7. **AI settings merge semantics** — `null` keeps, `""` clears (keys / guardrails / template); required fields (providers, base URLs, models) must be sent on every PUT.
8. **Embedding model must be 768-D.** Warn on `dimensionsMatchSchema: false`; `text-embedding-3-small` will not work without a backend migration.
9. **Folder deletion requires an empty subtree** (FK `Restrict` → 500 otherwise). Delete deepest-first; also warn that folder deletion leaves orphaned content items (they are not deleted; scope stays dangling).
10. **Workspace deletion has no cascade** — folders/notes/etc. remain orphaned. Handle confirmation UX accordingly.
11. **API keys are write-only.** Never bind raw key values into the model; only send them when the user types a new one.
12. **`parentFolderId` omitted** on the folder list call = root-level folders only.
13. **`retrievedChunkIds`** on assistant messages: source ids are chunk ids (not note ids) and cannot be resolved to titles via the current API — the UI can show a "grounded in N sources" badge.
14. **Dates are UTC ISO strings** — display in local time.
15. **Guids everywhere** — treat ids as opaque strings in Angular.

---

## 8. Configuration & Environment

### 8.1 `appsettings.json` (StudyApp.Api)

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": { "Default": "Host=localhost;Port=5433;Database=studyapp;Username=postgres;Password=postgres" },
  "Otel": { "Endpoint": "http://localhost:4317" },
  "Ai": {
    "ChatBaseUrl": "http://localhost:11434",
    "ChatModel": "gemma4:31b-cloud",
    "EmbeddingBaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text"
  },
  "AllowedHosts": "*"
}
```

- `ConnectionStrings:Default` — Postgres (host port 5433 mapped by compose).
- `Ai:*` — **first-read defaults only.** Once AI settings are saved via the UI, the `AiSettings` DB row wins. (Note: `Guardrails`/`SystemPromptTemplate` appsettings keys are not bound — their defaults live in code (`AiDefaults`) and are only visible/editable via the API.)
- `Otel:Endpoint` — OTLP collector (SigNoz). Telemetry export failures are non-fatal.
- Opt-in flag `Db:MigrateOnStartup` (env var `Db__MigrateOnStartup`) — when `true`, EF migrations run automatically on boot. Docker Compose sets it to `true`; plain `dotnet run` leaves migrations manual (`dotnet ef database update`).

### 8.2 Docker Compose (`docker-compose.yml`)

| Service | Image / build | Ports (host:container) | Notes |
|---|---|---|---|
| `db` | `pgvector/pgvector:pg17` | `5433:5432` | user/pass/db = `postgres`/`postgres`/`studyapp`; healthcheck via `pg_isready`; named volume `pgdata` |
| `api` | build from `Dockerfile` | `5031:8080` | waits for healthy db; `Db__MigrateOnStartup=true`; connects to `Host=db;Port=5432`; AI base URLs → `http://host.docker.internal:11434`; bind-mount `./StudyApp.Api/files:/app/files` |
| `ollama` (optional profile) | `ollama/ollama:latest` | `11434:11434` | start with `docker compose --profile ollama up -d`; then point AI settings base URLs at `http://ollama:11434` |

Environment-variable overrides use the standard .NET double-underscore convention (`ConnectionStrings__Default`, `Ai__ChatBaseUrl`, `Otel__Endpoint`, `Db__MigrateOnStartup`).

### 8.3 Dockerfile

Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` (restore cached per-csproj layer, publish Release) → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime, `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "StudyApp.Api.dll"]`, ensures `/app/files` exists for document storage.

### 8.4 Development Tooling Endpoints

- `GET /openapi/v1.json` — OpenAPI document (Development only).
- `GET /scalar/v1` — Scalar interactive API reference (Development only). Useful for the frontend agent to try requests.
- CORS: only `http://localhost:4200` (policy `AllowAngular`).

---

## 9. Known MVP Limitations

These are deliberate v1 scope cuts — the frontend should design around them:

1. **No authentication/authorization.** Single-user app; no tokens, no user entity.
2. **No pagination/search** beyond scope filters. List endpoints return full arrays.
3. **No updates/deletes for ChatSessions and ChatMessages** (append-only conversations).
4. **Document embedding supports `.txt` only.** PDF/DOCX/XLSX/PNG/JPG upload and download work, but they are not indexed for RAG yet.
5. **Scribbles are not embedded** until an OCR/vision step fills `ExtractedText` (field exists, currently always `null`).
6. **No re-indexing endpoint.** Changing the embedding model does not re-embed existing chunks; re-save each source to refresh.
7. **In-memory event queue.** Pending ingestion events are lost on API restart.
8. **No cascade cleanup** for workspace/folder deletion; FK `Restrict` blocks deleting folders with subfolders.
9. **No retrieval-status UI data** — indexing progress/state is not exposed.
10. **Chat history truncation** is silent (last 12 messages for prompting; last 50 returned by the history endpoint).
11. **No streaming responses** — chat replies arrive as a single JSON response (`stream: false` everywhere).
12. **Fixed 768-dim embeddings** — tied to the `vector(768)` schema and requires a migration to change.
13. **Single global AI settings row** — no per-workspace AI overrides (workspace-level behavior is limited to Description/CustomInstructions, which feed the prompt).
14. **Deleting a folder or workspace leaves content orphaned** (no FK relationships from content to its scope).
