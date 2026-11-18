# Deferred items

Out-of-scope discoveries made while executing the phase. Each row names where it was found, what it is and what was done.

| Found in | Item | Status |
|----------|------|--------|
| 100-02 (fixture creation) | The Platform Group Members "Create New User" modal posts its fields as `CreateMember.Email`, `CreateMember.Name` and `CreateMember.GroupRole`, but the `CreateMember` action binds an unprefixed `CreateMemberViewModel`. A form submitted as built therefore always arrives with empty fields, fails validation and re-renders the Members page with the modal closed and no visible message. The integration tests post unprefixed names, so they do not see it. The fixture tool submits the same form with the `CreateMember.` prefix removed from its field names, which creates the account through the real endpoint. Found in `Areas/Platform/Views/Group/Members.cshtml` (modal inputs) against `GroupController.CreateMember`. | Not fixed in 100-02 (the plan changes no source). Needs an owner: bind the view-model with `[Bind(Prefix = "CreateMember")]` or post unprefixed names, and give the failed path a visible reopen or toast. Related to the plan that decides the failed-create behaviour. |
