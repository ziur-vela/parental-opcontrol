# Luna Chat API (v1)

Server contract for the Luna child app and parent dashboard. This document contains no real names, hostnames or keys.

## Authentication and scopes

| Caller | How to authenticate | Allowed paths |
|---|---|---|
| OpControl service / Helper | Device credential: `Authorization: Bearer <device-token>` | `/api/device/*` including `/api/device/luna/token` |
| Luna child app | Luna token: `Authorization: Bearer <luna-token>` | **only** `/api/luna/*` |
| Parent dashboard | Parent cookie (`ocp.auth`) + `RequestVerificationToken` on POST | parent `/api/*` |

Rules:

- Device credentials are **not** valid on `/api/luna/*`; the device scheme rejects those paths.
- Luna tokens are **not** valid anywhere outside `/api/luna/*`; the Luna scheme rejects other paths.
- Luna tokens are random, stored as a SHA-256 hash, bound to one device and its child, and valid for **15 minutes**. Get a new one from the service when it expires.
- Parent POSTs use cookie auth, family scope and antiforgery (`RequestVerificationToken` header). The server validates family ownership on every call.

Error shape for all endpoints: `{"error":"<localized message>"}`. HTTP status is `401` (missing/invalid auth), `403` (permission/family/feature disabled), `400` (validation), `404` (not found), `429` (rate limit) or `500`.

## Device side

### `POST /api/device/luna/token`

Device credential. Returns a Luna token for the device's child if Luna is enabled and the family's provider setup passed its last test.

Response `200`:

```json
{ "token": "…", "expiresUtc": "2026-10-24T18:30:00Z" }
```

Errors: `403` with `{"error":"…"}` when Luna is disabled for the child or the setup is not tested.

## Child side (Luna token)

All responses are JSON and localized to the child's language. Fixed child-facing texts are returned as `notice` / `warning` fields; the app may display them as-is.

### `GET /api/luna/status`

```json
{
  "enabled": true,
  "childFirstName": "Ana",
  "theme": "night",
  "messagesLeftToday": 42,
  "cooldownUntilUtc": null,
  "dailyLimitReached": false,
  "language": "en",
  "helplineText": "Please talk to a grown-up you trust about this. You are not alone.",
  "notice": null,
  "warning": null
}
```

`notice` carries a state message when Luna cannot answer: resting (device locked), cool-down, or limit reached. `warning` is set when 5 or fewer messages remain.

### `GET /api/luna/chats`

Returns the child's **non-hidden** chats, newest first (up to 100).

```json
[
  {
    "id": "0f8f…",
    "subject": "math",
    "title": "Fractions",
    "titleSetByChild": false,
    "hiddenByChild": false,
    "createdUtc": "2026-10-24T17:00:00Z",
    "lastMessageUtc": "2026-10-24T17:05:00Z"
  }
]
```

### `POST /api/luna/chats`

```json
{ "subject": "math" }
```

Subjects: `math`, `science`, `history`, `dictionary`, `languages`, `curious`.

Returns the new chat object. `400` for an invalid subject.

### `PATCH /api/luna/chats/{chatId}`

Rename and/or hide a chat. Both fields optional.

```json
{ "title": "My fractions", "hidden": true }
```

Returns the updated chat. A non-empty title marks `titleSetByChild`. Hidden chats remain visible to parents. `404` when the chat is not the child's.

### `GET /api/luna/chats/{chatId}/messages`

Returns up to 500 messages, oldest first. `text` is what the child saw: for Luna messages it is the moderated `ShownText`, never a blocked original or a hidden concern marker.

```json
[
  {
    "id": "…",
    "chatId": "…",
    "role": "child",
    "text": "Can you help me with fractions?",
    "createdUtc": "2026-10-24T17:01:00Z",
    "flag": "none"
  }
]
```

`flag`: `none`, `blocked`, `replyBlocked`, `urgent`.

### `POST /api/luna/chats/{chatId}/messages`

Rate limit: **10 requests per minute per child**.

```json
{ "text": "Can you help me with fractions?" }
```

Response `200`:

```json
{
  "chatId": "…",
  "childMessage": { "id": "…", "chatId": "…", "role": "child", "text": "…", "createdUtc": "…", "flag": "none" },
  "lunaMessage":  { "id": "…", "chatId": "…", "role": "luna",  "text": "…", "createdUtc": "…", "flag": "none" },
  "messagesLeftToday": 41,
  "limitReached": false,
  "cooldown": false,
  "urgent": false,
  "notice": null,
  "warning": null
}
```

Behaviour:

- Input is moderated before the chat model. A blocked message never reaches the model; later prompts see only `[a message was removed by the safety filter]`.
- Moderation error/timeout fails closed: `lunaMessage` is the friendly refusal and no chat model call is made.
- A flagged reply is retried once with a stricter instruction; a second flag returns the fixed fallback, keeps the original in the parent view only, and raises `luna-flagged`.
- Urgent input, an urgent classifier category, or a model concern marker returns the fixed urgent message plus the family helpline. The marker is stripped and never shown.
- 3 blocked messages in 10 minutes start a 15-minute cool-down; the response then has `cooldown: true` and a `notice`.
- Device locked → `notice` is the "Luna is resting" text and the model is not called.
- Daily limit reached → `limitReached: true`, no model call, `notice` contains the limit text. `POST /api/luna/more-requests` asks the parents.
- Monthly family token cap reached → no model call, `notice` explains the pause.
- `400` when the text is empty or over 1,000 characters.

### `POST /api/luna/more-requests`

No body. Creates one pending request per child per 12 hours and pushes/notifies parents.

```json
{
  "id": "…",
  "childId": "…",
  "createdUtc": "…",
  "status": "Pending",
  "grantedAmount": null,
  "handledBy": null
}
```

### `POST /api/luna/theme`

```json
{ "theme": "night" }
```

`night` or `gray`. Returns `{"ok":true}`.

## Parent side (cookie auth)

All parent endpoints require family scope. Review/limits/requests/chat access need `ParentPermissions.Luna` (owners always pass). Provider settings and `/settings/test` are **owner only**.

### `GET /api/luna/settings`

Returns the family provider setup. Keys are never returned, only `hasKey` and the last 4 characters.

```json
{
  "setup": "B",
  "chatProvider": "openrouter",
  "chatModel": "vendor/model",
  "hasChatKey": true,
  "chatKeyLast4": "1234",
  "hasModerationKey": true,
  "moderationKeyLast4": "5678",
  "lastTestUtc": "2026-10-24T17:00:00Z",
  "lastTestOk": true,
  "lastTestError": null,
  "monthlyTokenCap": null,
  "helplineText": "Please talk to a grown-up you trust about this. You are not alone."
}
```

`setup`: `A` (OpenAI, Anthropic, Gemini or DeepSeek chat + OpenAI moderation) or `B` (one OpenRouter key: chat + Llama Guard 4).

### `POST /api/luna/settings`

```json
{
  "setup": "B",
  "chatProvider": "openrouter",
  "chatModel": "vendor/model",
  "chatKey": "sk-…",
  "moderationKey": null,
  "monthlyTokenCap": 2000000,
  "helplineText": "…"
}
```

- `chatKey` / `moderationKey` are optional on update; omit to keep the saved key. New keys are encrypted with DataProtection protector `OpControl.Parental.Luna.Keys`.
- Changing setup/provider/model/key resets the last test; Luna cannot be enabled until the test passes.
- Setup A with OpenAI chat may reuse the chat key for moderation (`moderationKey` optional).
- `helplineText` max 500; empty restores the default short sentence (no phone numbers until verified).

### `POST /api/luna/settings/test`

Owner only. Makes one tiny real chat call and one tiny moderation call with the configured keys. For OpenRouter it first checks `https://openrouter.ai/api/v1/endpoints/zdr` for the chosen chat model and `meta-llama/llama-guard-4-12b`; every OpenRouter request (chat and Llama Guard) sends `"provider":{"zdr":true}`.

```json
{ "ok": false, "error": "The provider rejected the key. Check the key and try again." }
```

`200` for both outcomes; `lastTestUtc`, `lastTestOk` and `lastTestError` are stored.

### `GET /api/children/{childId}/luna`

```json
{
  "childId": "…",
  "enabled": true,
  "enabledByParentId": "…",
  "enabledUtc": "…",
  "underThirteenAcknowledgedUtc": null,
  "dailyMessageLimit": 60,
  "extraInstructions": null,
  "theme": "night",
  "setupValid": true,
  "requiresUnderThirteenAcknowledgement": false,
  "messagesLeftToday": 60,
  "extraAllowedToday": 0,
  "monthlyPromptTokens": 0,
  "monthlyCompletionTokens": 0,
  "monthlyTokenCap": null,
  "approxCostUsd": 0.0
}
```

### `POST /api/children/{childId}/luna`

```json
{ "enabled": true, "dailyMessageLimit": 60, "extraInstructions": "Be encouraging.", "underThirteenAcknowledged": false }
```

- Enabling requires `setupValid` (`lastTestOk == true`).
- Under-13 rule: when the family uses Setup A and the child is under 13 **or has no date of birth**, the request must set `underThirteenAcknowledged: true` the first time (or an acknowledgement must already exist). Otherwise `400`.
- `dailyMessageLimit` 1–500, default 60. Extra instructions max 1,000 characters.
- Returns the child Luna object above.

### Chats and messages

- `GET /api/children/{childId}/luna/chats` — all chats, including hidden, newest first.
- `GET /api/children/{childId}/luna/chats/{chatId}` — returns `{"chat":{…},"messages":[…]}`, parents see both `text` (raw) and `shownText` (what the child saw), `flag`, `flagCategories`, `flagScores`, `reviewedUtc`, `reviewedByParentId`, `markedFine`, token counts.
- `POST /api/children/{childId}/luna/chats/{chatId}/delete` — parents can delete (children can only hide).
- `POST /api/children/{childId}/luna/clear` — deletes all Luna chats and messages for the child.

### `POST /api/children/{childId}/luna/messages/{messageId}/review`

```json
{ "markedFine": true }
```

Sets `reviewedUtc`, `reviewedByParentId` and `markedFine` ("This was fine").

### `POST /api/children/{childId}/luna/extra`

Grant extra messages for **today only** (1–500):

```json
{ "amount": 10 }
```

Response: `{"ok":true,"messagesLeftToday":20}`. Closes the latest pending more-request as `Granted`.

### `GET /api/children/{childId}/luna/requests`

```json
[
  { "id": "…", "childId": "…", "createdUtc": "…", "status": "Pending", "grantedAmount": null, "handledBy": null }
]
```

### `POST /api/children/{childId}/luna/more-requests/{requestId}/dismiss`

Sets the request to `Dismissed`. Returns `{"ok":true}`.

### `GET/POST /api/children/{childId}/luna/profile`

Learner profile, visible and editable by parents.

```json
{ "childId": "…", "text": "…", "updatedUtc": "…", "updatedBy": "luna" }
```

POST body: `{"text":"…"}` (max 4,000). Parent edits set `updatedBy` to the parent id.

### `GET /api/children/{childId}/luna/usage`

```json
{
  "promptTokens": 0,
  "completionTokens": 0,
  "totalTokens": 0,
  "approxCostUsd": 0.0,
  "monthlyTokenCap": null,
  "capReached": false
}
```

The counter is per **family** for the current local month. `approxCostUsd` is an estimate from constant per-million rates in code, not a billing figure.

## Alerts and push

New alert types:

| Type | When | Email/push rules |
|---|---|---|
| `luna-flagged` | blocked message, blocked Luna reply, cool-down, monthly cap | respects quiet hours; at most one per chat per 30 minutes for the normal/reply cases; push category `luna` |
| `luna-urgent` | urgent classifier category or model concern marker | immediate email + push, ignores quiet hours; push body has no message content: "Urgent Luna alert for {child}. Open the dashboard." |
| `luna-more-request` | child asks for more messages | normal alert, quiet hours apply; push category `luna` |

`PushPreference.Luna` (default on) controls the `luna` push category; co-parents need `ParentPermissions.Luna`. `luna-urgent` always bypasses quiet hours.

## Provider and safety notes for clients

- Provider errors map to non-technical messages: bad key, no credit, model not found, rate limited, provider down, moderation timeout. The child always receives the fixed refusal, never a raw provider error.
- OpenAI moderation uses per-category score thresholds (not the `flagged` boolean). Llama Guard 4 categories map: `S3`, `S4`, `S11` urgent; `S1`, `S2`, `S3`, `S4`, `S9`, `S10`, `S11`, `S12`, `S14` blocked; `S5`, `S6`, `S7`, `S8`, `S13` pass on their own. Values live in `LunaSafety`.
- The core tutor prompt is versioned in code (`LunaPrompts.Version`); parent extra instructions are appended after it.
