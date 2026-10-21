# Deferred items

## Plan 93-01

- `QuestBoard.UnitTests.Migrator.MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails`
  fails (exit code 1 instead of 4) when the whole `Migrator` test group runs together, for one of its two
  theory cases (`apply` or `backup`), and passes when run alone. It fails the same way on an untouched
  checkout of the commit before this plan, so it is pre-existing and unrelated to the page settings work.
