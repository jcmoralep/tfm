# Blazor pitfalls met in this repo

| Pitfall | Where it bit | Rule |
|---|---|---|
| `Navigation.NotFound()` inside a static SSR page with the status-code re-execute middleware throws "renderer does not have a component with ID" (500) | Email pages with email off (PR #51) | Redirect with `Navigation.NavigateTo("/not-found?codigo=404", replace: true)`; `NotFound.razor` sets 404 for that query |
| `ElementReference.FocusAsync()` on an element removed by the last render throws `JSException` and the error boundary shows "Algo salió mal" | Hiding "Aprobar" after approval (PR #55) | Catch `JSDisconnectedException or JSException or InvalidOperationException` in focus helpers; or do not restore focus to a conditional element |
| Status `switch` in a page without a default arm breaks when an enum member is added | Registration statuses | Keep a `_` arm; append enum members at the end |
| Re-rendered input value echoes what the user typed after a rejection | Invite code field | Acceptable for the user's own non-secret input; clear secrets (passwords) explicitly |
| Static SSR form posts without the antiforgery token answer 400 (the rate limiter still counts them) | Rate limiter checks | Scripts must include the token to observe the 429 page |
| Fake assistant answers instantly, so loading states are invisible | Chat | Verify the loading state with Gemini or a deliberate delay in the fake only for manual checks |
