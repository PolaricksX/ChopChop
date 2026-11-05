# Pantry Client

The Vite/React client uses only the versioned Pantry HTTP contract (`/api/v1/pantry`) and public request/response DTOs.

## Run

```bash
npm install
npm run dev
npm test
npm run build
```

Set `VITE_API_URL` when the API is not served from the same origin. The UI provides search, type, availability and expiration filters, paging, accessible loading/empty/error states, validation, and add/edit/consume/unavailable/delete actions.
