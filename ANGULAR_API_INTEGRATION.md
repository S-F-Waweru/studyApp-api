# Angular → StudyApp API Integration Guide

Everything the Angular client needs to match the **current** backend.
Written against the API state of **22 October 2026**.

---

## 0. What changed — read this first

If the Angular app was built against an older API, these are the things that will break it.

| # | Change | Effect on Angular |
| --- | --- | --- |
| 1 | Chat sessions now take **`targets`** (a list), not a single scope | `POST /api/chat/sessions` body changed. Old `scopeId`/`scopeType` still accepted but is legacy. |
| 2 | New endpoint `GET /api/chat/sessions/{id}/scope` | Use it to show "this chat can see N pieces" and to debug empty answers. |
| 3 | `ChatMessageDto.sources` changed shape | Now an array of `RetrievedSource` (`label`, `chunkId`, `sourceId`, `sourceType`, `title`, `locator`, `headingPath`, `score`). |
| 4 | Sources are **persisted** | `GET .../messages` now returns sources for old assistant messages too. Parse them, don't assume `null`. |
| 5 | `DocumentDto` gained 6 fields | `contentType`, `indexStatus`, `chunkCount`, `pageCount`, `extractionError`, `indexedAt`. Build the upload UI around `indexStatus`. |
| 6 | Every JSON reply is wrapped in an envelope | If any old code expected a bare DTO or array, it must unwrap `data` first. |
| 7 | Folder list requires `workspaceId` | `GET /api/folders?workspaceId=...` — omitting it sends `Guid.Empty` and returns nothing. |
| 8 | `DELETE` / `PUT` return **204 with no body** | Do not `JSON.parse` these responses. |
| 9 | `GET /api/documents/{id}/content` is a raw file | Use `responseType: 'blob'`, not JSON. |
| 10 | Chat message `role` is **lowercase** | `"user"` / `"assistant"`, while every other enum in the API is PascalCase. |

---

## 1. Environment

| Setting | Value |
| --- | --- |
| API base URL (dev) | `http://localhost:5031` |
| API base URL (https profile) | `https://localhost:7237` |
| API base URL (docker compose) | `http://localhost:5031` |
| Angular dev server (CORS-allowed) | `http://localhost:4200` |
| OpenAPI document | `http://localhost:5031/openapi/v1.json` (Development only) |
| Interactive docs | `http://localhost:5031/scalar/v1` (Development only) |

**CORS is locked to `http://localhost:4200`.** If you serve Angular from another port
(`ng serve --port 4300`), every call fails with a CORS error that looks like a network failure.
Add the origin in `StudyApp.Api/Program.cs` under `AddCors`.

**No auth headers yet.** Do not build a token interceptor that sends `Authorization` — the API
ignores it. Phase 7 will add it.

```ts
// environment.ts
export const environment = {
  apiUrl: 'http://localhost:5031',
};
```

---

## 2. The envelope — one rule for every JSON endpoint

Every JSON response, success or failure, has this shape:

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
| --- | --- | --- |
| `isSuccess` | boolean | Trust this, not the HTTP status alone. |
| `statusCode` | number | Mirrors the HTTP status. |
| `data` | `T \| null` | The real payload. `null` on errors and on 204. |
| `message` | `string \| null` | Human-readable error text. **Do not match on this string** — several are copy-paste leftovers (`GET /api/workspaces/{id}` and `GET /api/folders/{id}` both return `"Note not found"`; folder update/delete return `"Scribble not found"`). |
| `errors` | `Record<string, string[]> \| null` | Only on 400 validation failures. |

**Exceptions to the envelope:**

1. `GET /api/documents/{id}/content` → raw file stream.
2. Any **204** response → no body at all.
3. 500 from the production exception handler → `application/problem+json`, not the envelope.

### TypeScript type

```ts
export interface ApiResponse<T> {
  isSuccess: boolean;
  statusCode: number;
  data: T | null;
  message?: string | null;
  errors?: Record<string, string[]> | null;
}
```

### Recommended: one unwrapping interceptor

```ts
// core/api.interceptor.ts
import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { catchError, map, throwError } from 'rxjs';

export class ApiError extends Error {
  constructor(
    public statusCode: number,
    message: string,
    public errors?: Record<string, string[]> | null,
  ) {
    super(message);
  }
}

export const apiInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event: HttpEvent<unknown>) => {
      if (event instanceof HttpResponse && event.body && typeof event.body === 'object') {
        const body = event.body as any;
        if ('isSuccess' in body) {
          if (!body.isSuccess) throw new ApiError(body.statusCode, body.message ?? 'Request failed', body.errors);
          return event.clone({ body: body.data });
        }
      }
      return event;
    }),
    catchError((err: unknown) => {
      // an ApiError thrown above arrives here wrapped by Angular; re-throw it untouched
      if (err instanceof ApiError) return throwError(() => err);
      if (err instanceof HttpErrorResponse) {
        const body = err.error;
        const message = body?.message ?? err.message ?? 'Network error';
        return throwError(() => new ApiError(err.status, message, body?.errors));
      }
      return throwError(() => err);
    }),
  );
```

Register it once:

```ts
// app.config.ts
provideHttpClient(withInterceptors([apiInterceptor]))
```

With the interceptor in place, every service call returns the **unwrapped** `data`, so service
signatures read `Observable<WorkspaceDto[]>`, not `Observable<ApiResponse<WorkspaceDto[]>>`.
If you prefer no interceptor, keep the envelope types and unwrap in each service.

**204 handling:** with the interceptor, `put`/`delete` bodies are `null`. Type them as
`Observable<void>` and never touch `.data`.

---

## 3. Enum values (send and expect these exact strings)

The API serializes enums as **strings** using the declared C# names.

| Enum | Values |
| --- | --- |
| `ScopeType` | `"Workspace"`, `"Folder"` |
| `ChatScopeType` | `"Workspace"`, `"Folder"`, `"Document"`, `"Note"`, `"Scribble"` |
| `AiProviderType` | `"Ollama"`, `"OpenAiCompatible"` |
| `IndexStatus` | `"Pending"`, `"Extracting"`, `"Indexed"`, `"Unchanged"`, `"Skipped"`, `"Failed"` |
| Chat message `role` | `"user"`, `"assistant"` — **lowercase, deliberately different** |

`ScopeType` and `ChatScopeType` are **not interchangeable**. A note is filed at
`ScopeType.Workspace` or `ScopeType.Folder`; a chat target can additionally be a
`ChatScopeType.Document`, `.Note` or `.Scribble`.

The API reads enums case-insensitively, but always write the exact casing above — do not rely
on the tolerance.

---

## 4. Complete TypeScript model file

```ts
// models/api.models.ts

export interface ApiResponse<T> {
  isSuccess: boolean;
  statusCode: number;
  data: T | null;
  message?: string | null;
  errors?: Record<string, string[]> | null;
}

// ---------- enums ----------
export type ScopeType = 'Workspace' | 'Folder';
export type ChatScopeType = 'Workspace' | 'Folder' | 'Document' | 'Note' | 'Scribble';
export type AiProviderType = 'Ollama' | 'OpenAiCompatible';
export type IndexStatus = 'Pending' | 'Extracting' | 'Indexed' | 'Unchanged' | 'Skipped' | 'Failed';
export type ChatRole = 'user' | 'assistant';

// ---------- workspaces ----------
export interface WorkspaceDto {
  id: string;
  name: string;
  description: string | null;
  customInstructions: string | null;
  createdAt: string; // ISO 8601 UTC
}

export interface CreateWorkspaceRequest {
  name: string;                        // required
  description?: string | null;
  customInstructions?: string | null;
}

export interface UpdateWorkspaceRequest {
  name: string;                        // required
  description?: string | null;
  customInstructions?: string | null;
}

// ---------- folders ----------
export interface FolderDto {
  id: string;
  workspaceId: string;
  parentFolderId: string | null;       // null = root folder of the workspace
  name: string;
  createdAt: string;
}

export interface CreateFolderRequest {
  workspaceId: string;
  parentFolderId?: string | null;
  name: string;
}

export interface UpdateFolderRequest {
  name: string;
}

// ---------- notes ----------
export interface NoteDto {
  id: string;
  scopeId: string;
  scopeType: ScopeType;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateNoteRequest {
  scopeId: string;
  scopeType: ScopeType;
  title: string;
  content: string;
}

export interface UpdateNoteRequest {
  title: string;
  content: string;
}

// ---------- documents ----------
export interface DocumentDto {
  id: string;
  scopeId: string;
  scopeType: ScopeType;
  filename: string;
  createdAt: string;
  contentType: string | null;
  indexStatus: IndexStatus;
  chunkCount: number;
  pageCount: number | null;
  extractionError: string | null;
  indexedAt: string | null;
}

// ---------- scribbles ----------
export interface ScribbleDto {
  id: string;
  scopeId: string;
  scopeType: ScopeType;
  title: string;
  canvasData: string;                  // raw Excalidraw scene JSON, as a string
  extractedText: string | null;        // always null today; the index stores the text instead
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateScribbleRequest {
  scopeId: string;
  scopeType: ScopeType;
  title: string;
  canvasData: string;
}

export interface UpdateScribbleRequest {
  title: string;
  canvasData: string;
}

// ---------- chat ----------
export interface ChatScopeTargetDto {
  targetType: ChatScopeType;
  targetId: string;
  includeLinked: boolean;              // note <-> scribble links
  includeAncestors: boolean;           // also widen to the owning folder / workspace
}

export interface ChatSessionDto {
  id: string;
  scopeId: string;                     // owning location, used for grouping in the sidebar
  scopeType: ScopeType;
  title: string;
  createdAt: string;
  targets: ChatScopeTargetDto[] | null; // NOTE: null when listed via GET /api/chat/sessions
}

export interface CreateChatSessionRequest {
  title: string;                       // required, max 300 chars
  targets?: ChatScopeTargetDto[];      // at least one required
  scopeId?: string | null;             // legacy single-scope path
  scopeType?: ScopeType | null;        // legacy single-scope path
}

export interface RetrievedSource {
  label: string;                       // "S1", "S2" ...
  chunkId: string;
  sourceId: string;                    // document / note / scribble id
  sourceType: 'document' | 'note' | 'scribble';
  title: string | null;
  locator: string | null;              // "p.42", "slide 3", "sheet:Overview", "frame:Networking"
  headingPath: string | null;          // "Kubernetes > Networking > Services"
  score: number;                       // reciprocal-rank-fusion score: ~0.016 (one list) to ~0.033 (both).
                                       // Higher is better. NOT a percentage or cosine similarity.
}

export interface ChatMessageDto {
  id: string;
  role: ChatRole;
  content: string;
  retrievedChunkIds: string[];
  createdAt: string;
  sources: RetrievedSource[] | null;   // assistant messages only
}

export interface SendMessageRequest {
  content: string;                     // required
}

export interface ChatScopeSummaryDto {
  sessionId: string;
  declaredTargets: number;
  resolvedChunks: number;              // 0 = the chat will answer "no context"
  targets: ChatScopeTargetDto[];
}

// ---------- ai settings ----------
export interface AiSettingsDto {
  chatProvider: AiProviderType;
  chatBaseUrl: string;
  chatModel: string;
  chatApiKeySet: boolean;              // never the key itself
  chatApiKeyPreview: string | null;    // "••••abcd"
  guardrails: string;                  // resolved: DB value or built-in default
  systemPromptTemplate: string;        // resolved: DB value or built-in default
  embeddingProvider: AiProviderType;
  embeddingBaseUrl: string;
  embeddingModel: string;
  embeddingApiKeySet: boolean;
  embeddingApiKeyPreview: string | null;
  embeddingDimensions: number;         // fixed at 768 by the vector column
  updatedAt: string;
}

export interface UpdateAiSettingsRequest {
  chatProvider: AiProviderType;
  chatBaseUrl: string;                 // required
  chatModel: string;                   // required
  chatApiKey: string | null;           // null = keep, "" = clear, value = replace
  embeddingProvider: AiProviderType;
  embeddingBaseUrl: string;            // required
  embeddingModel: string;              // required
  embeddingApiKey: string | null;
  embeddingDimensions?: number | null; // must be 768 if sent
  guardrails?: string | null;          // null = keep, "" = reset to default
  systemPromptTemplate?: string | null;
}

export interface TestAiRequest {
  chatProvider?: AiProviderType | null;
  chatBaseUrl?: string | null;
  chatApiKey?: string | null;
  chatModel?: string | null;
  embeddingProvider?: AiProviderType | null;
  embeddingBaseUrl?: string | null;
  embeddingApiKey?: string | null;
  embeddingModel?: string | null;
}

export interface ChatConnectionResult {
  success: boolean;
  message: string;
  models: string[];
  modelAvailable: boolean;
}

export interface EmbeddingConnectionResult {
  success: boolean;
  message: string;
  dimensions: number | null;
  dimensionsMatchSchema: boolean;
}

export interface AiTestReport {
  chat: ChatConnectionResult;
  embeddings: EmbeddingConnectionResult;
}
```

---

## 5. Endpoint reference

All paths are relative to `environment.apiUrl`. All UUIDs are strings.
All dates are ISO 8601 UTC strings (`2026-10-22T10:31:13.000Z`).

### 5.1 Workspaces — `/api/workspaces`

#### `GET /api/workspaces`
List every workspace.
- **Response 200:** `WorkspaceDto[]`
- **Angular:** load once at app start for the workspace switcher.

#### `GET /api/workspaces/{id}`
- **Response 200:** `WorkspaceDto`
- **404:** `isSuccess:false`, `message:"Note not found"` ← known copy-paste bug; treat any
  `isSuccess:false` as not-found and do not display `message` verbatim.

#### `POST /api/workspaces`
- **Body:** `CreateWorkspaceRequest`
- **201:** `WorkspaceDto` + `Location` header pointing at the new resource
- **400:** validation `errors` when `name` is missing

#### `PUT /api/workspaces/{id}`
- **Body:** `UpdateWorkspaceRequest` (full replace of the editable fields)
- **204:** no body
- **404:** not found

#### `DELETE /api/workspaces/{id}`
- **204:** no body
- **404:** not found

> **Destructive and incomplete — handle in the UI.** There is **no foreign key** from `Folders`,
> `Notes`, `Documents` or `Scribbles` to `Workspaces`. Deleting a workspace removes only the
> workspace row: its folders and items stay in the database as orphans, and their chunks stay in
> the index. Either hide workspace deletion until the backend cascades properly, or require the
> user to empty the workspace first.

---

### 5.2 Folders — `/api/folders`

#### `GET /api/folders?workspaceId={guid}&parentFolderId={guid}`
List the direct children of a folder.
- `workspaceId` — **required**. Omitting it binds `Guid.Empty` and returns an empty list.
- `parentFolderId` — optional. Omit it (or send nothing) to list the workspace root folders.
- **Response 200:** `FolderDto[]` — direct children only, not the whole subtree.

```ts
listFolders(workspaceId: string, parentFolderId?: string | null) {
  const params: Record<string, string> = { workspaceId };
  if (parentFolderId) params['parentFolderId'] = parentFolderId;
  return this.http.get<FolderDto[]>('/api/folders', { params });
}
```

#### `GET /api/folders/{id}/descendants`
- **Response 200:** `string[]` — the folder id **plus every descendant folder id** (flat list).
- Useful to pre-compute "how many notes live under this folder".
- Not the same as the chat scope expansion, which also includes items.

#### `GET /api/folders/{id}`
- **200:** `FolderDto` · **404:** not found

#### `POST /api/folders`
- **Body:** `CreateFolderRequest` — `{ workspaceId, parentFolderId | null, name }`
- **201:** `FolderDto` + `Location`

#### `PUT /api/folders/{id}`
- **Body:** `UpdateFolderRequest` — `{ name }` only. There is **no** move/reparent endpoint; a
  folder cannot be moved after creation.
- **204** · **404**

#### `DELETE /api/folders/{id}`
- **204** · **404**
- **500 if the folder has sub-folders.** `Folders.ParentFolderId` is a real foreign key with
  `DeleteBehavior.Restrict`, so deleting a parent folder throws a database error. Delete the tree
  bottom-up, deepest first.

> Notes, documents and scribbles filed in a folder hold only a loose `ScopeId` guid with **no**
> foreign key, so deleting their folder leaves them orphaned: still in the database, no longer
> reachable by browsing the folder tree. Move or delete the contents first.

---

### 5.3 Notes — `/api/notes`

#### `GET /api/notes?scopeId={guid}&scopeType={Workspace|Folder}`
- **Both query params are required.**
- **Response 200:** `NoteDto[]` — notes filed **directly** at that scope, not recursive.

#### `GET /api/notes/{id}`
- **200:** `NoteDto` · **404**

#### `POST /api/notes`
- **Body:** `CreateNoteRequest` — `{ scopeId, scopeType, title, content }`
- **201:** `NoteDto` + `Location`
- Fires a background re-index of the note. Nothing to wait for in the UI.

#### `PUT /api/notes/{id}`
- **Body:** `UpdateNoteRequest` — `{ title, content }`
- **204** · **404**
- Debounce autosave (2–3 s idle). Every save publishes an indexing event. The worker deduplicates
  by content hash, so identical saves are cheap, but notes currently re-embed on every real change.

#### `DELETE /api/notes/{id}`
- **204** · **404**
- Removes the note's chunks in the background.

---

### 5.4 Documents — `/api/documents`

#### `POST /api/documents/upload`
`multipart/form-data`. Three form fields:

| Field | Type | Notes |
| --- | --- | --- |
| `scopeId` | string (guid) | owning workspace or folder |
| `scopeType` | `"Workspace"` or `"Folder"` | |
| `file` | binary | the file itself |

- **Response 200** (not 201): `DocumentDto`
- **400:** validation

```ts
upload(scopeId: string, scopeType: ScopeType, file: File) {
  const form = new FormData();
  form.append('scopeId', scopeId);
  form.append('scopeType', scopeType);
  form.append('file', file);
  // note: no Content-Type header — the browser must set the multipart boundary
  return this.http.post<DocumentDto>('/api/documents/upload', form);
}
```

Supported types are detected from **magic bytes**, not the extension:
PDF, DOCX, PPTX, XLSX, Markdown, plain text, images.
Anything else is accepted but ends in `indexStatus: "Failed"` with an `extractionError`.

#### `GET /api/documents?scopeId={guid}&scopeType={Workspace|Folder}`
- **200:** `DocumentDto[]`
- This is the polling endpoint for indexing progress.

#### `DELETE /api/documents/{id}`
- **204** · **404**
- Deletes the stored file, the database row and (in the background) its chunks.

#### `GET /api/documents/{id}/content`
- **200:** the raw file bytes, with `Content-Type` and `Content-Disposition: attachment; filename=...`
- **404:** JSON envelope (this one *does* use the envelope)
- **This is not JSON.** Always request it as a blob.

```ts
download(id: string) {
  return this.http.get(`/api/documents/${id}/content`, {
    observe: 'response',
    responseType: 'blob',
  });
  // filename: parse `content-disposition`, fallback to the DocumentDto.filename
}
```

**Polling recipe** — after upload, poll the scope list every 2–3 s until every document leaves
`Pending`/`Extracting`:

| `indexStatus` | Meaning in the UI |
| --- | --- |
| `Pending` | queued, not started |
| `Extracting` | reading the file, calling the vision model on figures |
| `Indexed` | searchable — show `chunkCount` and `pageCount` |
| `Skipped` | nothing extractable (empty canvas, empty file) |
| `Failed` | show `extractionError` |
| `Unchanged` | reserved; the worker currently skips silently instead of emitting it |

Stop polling after ~2 minutes and show "still processing" rather than spinning forever.

---

### 5.5 Scribbles — `/api/scribbles`

A scribble is an Excalidraw scene stored as a **string**, not a parsed object.

#### `GET /api/scribbles?scopeId={guid}&scopeType={Workspace|Folder}`
- **200:** `ScribbleDto[]` — direct children only.

#### `GET /api/scribbles/{id}`
- **200:** `ScribbleDto` · **404**

#### `POST /api/scribbles`
- **Body:** `CreateScribbleRequest` — `{ scopeId, scopeType, title, canvasData }`
- **201** + `Location`
- `canvasData` must be `JSON.stringify(scene)`, or `"{}"` for an empty canvas.

#### `PUT /api/scribbles/{id}`
- **Body:** `UpdateScribbleRequest` — `{ title, canvasData }`
- **204** · **404**
- Autosave this on a **debounce of at least 5 s**, and only when Excalidraw reports a change.
  The server compares an extracted-content fingerprint, so pure layout moves are skipped — but
  every request still travels to the server and triggers a hash pass.

#### `DELETE /api/scribbles/{id}`
- **204** · **404**

#### `POST /api/scribbles/{id}/link/{noteId}`
#### `DELETE /api/scribbles/{id}/link/{noteId}`
- **204** on both, no body
- Creates / removes a `NoteScribbleLink`. This is what `includeLinked` uses when a chat is scoped
  to a note. Deleting a link does not re-index anything.

---

### 5.6 Chat — `/api/chat`

#### `POST /api/chat/sessions`
Create a scoped conversation.

- **Body:** `CreateChatSessionRequest`
  ```json
  {
    "title": "Kubernetes networking",
    "targets": [
      { "targetType": "Document", "targetId": "...", "includeLinked": false, "includeAncestors": false },
      { "targetType": "Folder",   "targetId": "...", "includeLinked": false, "includeAncestors": true  }
    ]
  }
  ```
- **200:** `ChatSessionDto` (with `targets` populated) — note it is 200, not 201
- **400:** `"A chat session needs at least one scope target."` or
  `"Chat scope target Document/... no longer exists."`

Legacy single-scope body still works:
```json
{ "title": "...", "scopeId": "...", "scopeType": "Folder" }
```

**Target guidance for the UI**

| Target | `includeAncestors` | `includeLinked` | Result |
| --- | --- | --- | --- |
| `Workspace` | ignored | ignored | whole workspace |
| `Folder` | `true` | ignored | folder + sub-folders + every ancestor folder |
| `Document` | `true` | ignored | this document + its whole folder |
| `Note` | `true` | `true` | note + linked scribbles + its folder |
| `Scribble` | `true` | `true` | scribble + linked notes + its folder |

#### `GET /api/chat/sessions?scopeId={guid}&scopeType={Workspace|Folder}`
- **Both params required.**
- **200:** `ChatSessionDto[]` — chronological, **`targets` is `null` here**.
- Use it for the sidebar list; fetch a single session's targets only if you need them.

#### `GET /api/chat/sessions/{id}/scope`
- **200:** `ChatScopeSummaryDto`
- `resolvedChunks: 0` means the chat has nothing to read. Show a warning banner instead of
  letting the user send a message that will 409.

```json
{
  "sessionId": "...",
  "declaredTargets": 2,
  "resolvedChunks": 148,
  "targets": [ { "targetType": "Document", "targetId": "...", "includeLinked": false, "includeAncestors": false } ]
}
```

#### `GET /api/chat/sessions/{id}/messages`
- **200:** `ChatMessageDto[]` — oldest first, both roles, sources included for assistant messages.
- Default page size is 50, newest 50 (`count` is a service parameter, not exposed on the route).

#### `POST /api/chat/sessions/{id}/messages`
- **Body:** `{ "content": "..." }`
- **200:** the **assistant** message only (`ChatMessageDto`); the user message is stored but not returned
- **409:** `"The selected chat scope no longer contains any material."` — show this as
  "everything in this chat was deleted or unindexed", and offer to re-scope or delete the session
- **404:** session not found

**This call is slow.** It embeds the question, runs vector + keyword search, builds the prompt and
waits for the model. The server allows up to 5 minutes for the LLM call. Give the Angular request a
matching timeout (`withTimeout(...)`) or none at all, and show a typing indicator immediately.

If the model is offline you still get **200** with content starting `(Assistant unavailable:` —
the history stays consistent. Detect that prefix and style it as a system notice.

**Rendering sources**

```html
@for (s of message.sources ?? []; track s.chunkId) {
  <button class="citation" (click)="open(s.sourceId, s.locator)">
    [{{ s.label }}] {{ s.title ?? s.sourceType }}
  </button>
}
```

- `label` is what the model cites in the text (`[S1]`), so the safest UI is to regex
  `/\[S(\d+)\]/g` in `content` and turn each match into a chip bound to `sources[i - 1]`.
- `locator` gives the jump target: `p.42` (PDF), `slide 3` (PPTX), `sheet:Overview` (XLSX),
  `frame:Networking` (scribble frame).
- `sourceType` tells you which viewer to open: `document` → blob viewer, `note` → note editor,
  `scribble` → Excalidraw.

---

### 5.7 AI settings — `/api/ai`

#### `GET /api/ai/settings`
- **200:** `AiSettingsDto`
- The API key is **never** returned. Use `chatApiKeySet` + `chatApiKeyPreview` for the field:
  show the preview as a placeholder and an empty input meaning "unchanged".

#### `PUT /api/ai/settings`
- **Body:** `UpdateAiSettingsRequest`
- **200:** updated `AiSettingsDto`
- **400** on: blank chat base URL/model, blank embedding base URL/model, or
  `embeddingDimensions !== 768`.

Field semantics — this is the part to get right in the form:

| Field | Send `null` | Send `""` | Send a value |
| --- | --- | --- | --- |
| `chatApiKey` / `embeddingApiKey` | keep stored key | **clear** the key | replace |
| `guardrails` / `systemPromptTemplate` | keep stored | reset to built-in default | replace |

So the form should send `null` for a key the user did not touch — never send `""` unless the user
clicked "remove key".

> **Warning about `systemPromptTemplate`:** it must keep the placeholders `{guardrails}`,
> `{workspace_context}` and `{retrieved_context}`. If they are removed, the retrieved context is
> silently dropped and the chat answers from the model's memory only. Validate in the form.

`chatBaseUrl` / `embeddingBaseUrl` are stored with the trailing slash trimmed.

#### `POST /api/ai/test`
- **Body:** `TestAiRequest`. Send `{}` to test the **saved** settings; fill fields to test unsaved
  form values without persisting. An empty request body is rejected with 400, so always send at
  least `{}`.
- **200:** `AiTestReport`
- Never throws for a bad endpoint — failures come back as `success: false` with a `message`.

```json
{
  "chat":       { "success": true, "message": "...", "models": ["gemma4:31b-cloud"], "modelAvailable": true },
  "embeddings": { "success": true, "message": "...", "dimensions": 768, "dimensionsMatchSchema": true }
}
```

Show `dimensionsMatchSchema: false` as a blocking error: the `vector(768)` column cannot store
other sizes without a migration.

---

### 5.8 Indexing — `/api/indexing`

Maintenance endpoints, useful after changing the embedding model or when a file looks stale.

#### `POST /api/indexing/reindex/{sourceType}/{sourceId}`
- `sourceType` ∈ `document` | `scribble` | `note` (case-insensitive)
- **200:** `{ "queued": 1 }` (0 when the source does not exist)
- Queues one background re-index; returns immediately.

#### `POST /api/indexing/reindex`
- **200:** `{ "queued": 37 }`
- Queues up to 500 sources whose chunks were produced by an older pipeline version.

---

## 6. Flows to implement

### 6.1 Upload and watch it become searchable

```ts
this.documents.upload(scopeId, scopeType, file).subscribe(doc => {
  this.docs.update(list => [doc, ...list]);          // indexStatus: "Pending"
  this.poll(doc);
});

private poll(doc: DocumentDto, tries = 0) {
  if (['Indexed', 'Skipped', 'Failed'].includes(doc.indexStatus) || tries > 40) return;
  timer(2500).pipe(take(1)).subscribe(() =>
    this.documents.list(this.scopeId, this.scopeType).subscribe(list => {
      const next = list.find(d => d.id === doc.id);
      if (next) { this.docs.update(d => d.map(x => x.id === next.id ? next : x)); this.poll(next, tries + 1); }
    }));
}
```

### 6.2 Create a scoped chat from the current view

The natural mapping from UI context to targets:

| Where the user clicked "New chat" | Targets to send |
| --- | --- |
| Workspace home | `[{ targetType: 'Workspace', targetId: wsId, includeLinked: false, includeAncestors: false }]` |
| Inside a folder | `[{ targetType: 'Folder', targetId: folderId, includeLinked: false, includeAncestors: false }]` |
| A single note open | `[{ targetType: 'Note', targetId: noteId, includeLinked: true, includeAncestors: false }]` |
| A single document open | `[{ targetType: 'Document', targetId: docId, includeLinked: false, includeAncestors: false }]` |
| A scribble open | `[{ targetType: 'Scribble', targetId: scribbleId, includeLinked: true, includeAncestors: false }]` |
| Multi-select | one entry per selected item |

After creating, call `GET .../scope` once and disable the send box if `resolvedChunks === 0`.

### 6.3 Send a message

```ts
send(sessionId: string, content: string) {
  this.messages.update(m => [...m, { id: crypto.randomUUID(), role: 'user', content,
                                     retrievedChunkIds: [], createdAt: new Date().toISOString(),
                                     sources: null } as ChatMessageDto]);
  return this.chat.sendMessage(sessionId, { content }).pipe(
    tap(reply => this.messages.update(m => [...m.slice(0, -1), reply])),   // swap the optimistic copy
  );
}
```

Always refetch `GET .../messages` when reopening a session rather than relying on local state —
the persisted `sources` are the only copy for old answers.

### 6.4 Settings screen

1. `GET /api/ai/settings` → bind everything except the keys.
2. Keys: empty input + placeholder `chatApiKeyPreview`; track a `keyChanged` flag per field.
3. On save, send `chatApiKey: keyChanged ? value : null`.
4. Offer a "Restore default prompt" button that sends `systemPromptTemplate: ""`.
5. `POST /api/ai/test` before saving, and show `chat.modelAvailable` and
   `embeddings.dimensionsMatchSchema` as separate checks.

---

## 7. Gotchas checklist

- [ ] Unwrap `data` on every JSON call.
- [ ] Treat 204 as empty — no `.data`, no JSON parse.
- [ ] `role` is lowercase; every other enum is PascalCase.
- [ ] `ScopeType` (`Workspace|Folder`) ≠ `ChatScopeType` (adds Document/Note/Scribble).
- [ ] Folder list needs `workspaceId`; chat session list needs `scopeId` **and** `scopeType`.
- [ ] Folder list and note list are **not recursive**. Use `descendants` for folder trees.
- [ ] `GET /api/chat/sessions` returns `targets: null` — fetch or create when you need them.
- [ ] `POST .../messages` returns only the assistant message.
- [ ] Handle **409** on send: the scope emptied. Do not retry blindly.
- [ ] Handle the `(Assistant unavailable:` prefix — it is a 200, not an error.
- [ ] `GET /api/documents/{id}/content` needs `responseType: 'blob'`.
- [ ] Upload body is `FormData` with `scopeId`, `scopeType`, `file`.
- [ ] Poll `indexStatus`; stop at Indexed/Skipped/Failed.
- [ ] Never send `""` for an API key unless the user asked to clear it.
- [ ] Keep the three `{placeholders}` in the prompt template.
- [ ] `embeddingDimensions` must stay `768`.
- [ ] CORS only allows `http://localhost:4200`.
- [ ] Deleting a folder with sub-folders returns **500** (FK is `Restrict`) — delete deepest first.
- [ ] Deleting a workspace or folder orphans its contents (no FK) — confirm with the user, or block it.
- [ ] Debounce note saves (~2–3 s) and scribble autosave (≥5 s).

---

## 8. Not implemented — do not design for these yet

| Feature | Status |
| --- | --- |
| Authentication / login screen | No endpoints. Every route is open. |
| Authorisation / per-user workspaces | `Workspaces.OwnerUserId` exists but is never set or checked. |
| MFA | Nothing. |
| Vision model settings | Columns exist; not exposed by `/api/ai/settings`. Figures use the chat model. |
| Document replace endpoint | `ReplaceAsync` exists in the service but has **no** controller route. Re-upload as a new document. |
| Cascading delete | Only `ChatSessionScopes → ChatSessions` cascades. Workspace and folder deletes leave orphaned rows. |
| Email/notifications | None. |
