# Code Navigation — RIP MCP

If the `rip` MCP server is available (tools prefixed `mcp__rip__`), **always prefer it over reading files** for any symbol-navigation question. It has the full codebase indexed.

| Goal | Tool |
|---|---|
| Find where a symbol is defined | `FindDefinition` |
| Find a symbol by name (partial or exact) | `FindSymbol` |
| Find every usage of a symbol across the codebase | `FindReferences` |
| Read the source body of a function/class | `GetSymbolBody` |
| List all fields and methods of a class | `GetClassMembers` |
| List all values of an enum | `GetEnumValues` |
| Who calls a function | `FindCallers` |
| What does a function call | `FindCallees` |
| Subclasses / implementors of a base | `FindImplementations` |
| Full inheritance chain | `FindInheritanceTree` |
| Trace a dependency path between two symbols | `FindDependencyPath` |
| High-level subsystem dependency map | `GetArchitectureSummary` |

## RIP Lookup Protocol

When a user asks about a feature, system, or concept by name — even if the term is not obviously a symbol (e.g. "sota system", "payment flow") — follow this sequence:

1. **`GetArchitectureSummary`** — identify which namespaces/subsystems relate to the term
2. **`FindSymbol`** — try PascalCase variants: `sota` → `SotaHandler`, `Sota`, `SotaRequest`; try the plural, the base class name, the interface name
3. **`GetClassMembers`** on each found type — get structure without reading files
4. **`GetSymbolBody`** for specific methods of interest
5. **`FindCallers` / `FindCallees`** to trace integrations
6. **`FindImplementations`** for interfaces or base classes

**Only after all of the above yield nothing:** use `Grep` with `output_mode: files_with_matches` to find file paths, then apply RIP tools (`GetSymbolBody`, `GetClassMembers`) to symbols found in those files. **Never `Read` a whole file** when RIP can answer the question.

One failed `FindSymbol` query is not a reason to fall back — try at least 3 symbol-name variants before giving up on RIP.

**When RIP is insufficient**, before falling back to file reads, output a short notice in this exact format so Thomas can improve the index:

```
⚠ RIP gap send to Thomas
Query   : <tool name> / <symbol or query used>
Reason  : <one sentence: why RIP couldn't answer — e.g. "symbol not indexed", "enum values missing", "FindCallers returned empty for X">
Fallback: <what you are doing instead>
```

Then continue with the fallback. Do not block on this — emit the notice and proceed.
