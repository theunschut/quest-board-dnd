# API Coverage — Resend

> Full coverage by default. Opt-outs are explicit, reasoned decisions.

This phase touches one external API, Resend, and only the existing read behind Admin/EmailStats
(`ResendStatsClient` through the named `Resend` HTTP client). It adds a test stub for that read and, on the operator's
O-13 answer, a base-address setting honoured only in the Development environment for a dev-only capture (Production and
Testing always use the fixed Resend address); it sends nothing through Resend and adds no new
call. The detector matched the phrase ".NET SDK" in an environment note and "Resend API" in the threat models; the
matrix below records the decision for every capability of the Resend surface.

| capability | decision | reason |
|---|---|---|
| emails.list (GET /emails, the delivery records EmailStats aggregates) | INTEGRATE | existing read; exercised by the canned handler in tests and the dev-only stub capture, never with a real key |
| emails.get (one email by id) | OPT-OUT | not needed: the stats page aggregates the list and never opens one record |
| emails.send | OPT-OUT | explicitly out of scope: the app sends mail through its own SMTP pipeline, and this phase sends nothing (suppression forced) |
| emails.batch send | OPT-OUT | explicitly out of scope: same as emails.send |
| emails.update and emails.cancel (scheduled emails) | OPT-OUT | not needed: the app schedules nothing through Resend |
| domains (create, get, verify, update, list, delete) | OPT-OUT | explicitly out of scope: domain setup is the operator's account administration, not an app feature |
| api-keys (create, list, delete) | OPT-OUT | explicitly out of scope: the key is operator configuration and this phase uses only a stub value in tests |
| audiences and contacts | OPT-OUT | not needed: the app keeps its own member list and sends no marketing mail |
| broadcasts | OPT-OUT | not needed: no bulk or marketing mail in this product |
| webhooks (delivery events) | OPT-OUT | not needed yet: the stats page reads the list on demand with a five-minute cache; event push is not in this phase's scope |
