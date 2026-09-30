# senssera-web

Next.js 16 frontend for SensSera. Project overview, architecture and the full-stack Docker setup are in the [root README](../README.md).

## Run

```bash
npm install
npm run dev        # http://localhost:3000, expects the API on http://localhost:5010
```

## Environment

| Variable | Where | Purpose |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | local, docker compose | API base URL the browser calls directly. Inlined at build time. |
| `API_ORIGIN` | Vercel | Azure API origin. `next.config.ts` proxies `/api` and `/hubs` to it, so the browser stays same-origin and the refresh cookie is first-party. |

On Vercel `NEXT_PUBLIC_VERCEL_ENV` is set automatically and the client uses relative URLs (`lib/config.ts`).

## Scripts

| Script | |
|---|---|
| `npm run lint` / `npx tsc --noEmit` / `npm test` / `npm run build` | The checks CI runs. |
| `npm run gen:api` | Regenerates `lib/api-schema.ts` from the running API's OpenAPI document. `lib/types.ts` derives the domain types from it. |
| `npm run hero` | Re-renders the static hero fallbacks in `public/hero/` with the locally installed Chrome. |

## Layout

- `app/`: routes. `(auth)` holds sign-in and sign-up, `(app)` the authenticated pages behind the auth guard and the shared SignalR connection.
- `lib/`: API client (in-memory access token, single-flight refresh), auth context, SignalR, types, band math.
- `hooks/`: TanStack Query hooks, one file per resource.
- `components/notebook/`: the design's signature pieces (`BandGauge`, `FieldNote`, `SpecimenHeader`, `LiveStamp`).
- `components/ui/`: shadcn/ui primitives, restyled through the tokens in `app/globals.css`.
- `lib/hero/greenhouse-scene.ts`: the live three.js clay greenhouse on the sign-in page.
