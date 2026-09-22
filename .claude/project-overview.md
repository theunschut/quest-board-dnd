# Project

**D&D Quest Board — v9.0 Rolling Improvements**

A D&D campaign management web application for a group of players and Dungeon Masters. It handles
quest creation and scheduling, player signup with date voting, a character/guild system, a shop with
gold economy, and email notifications. Built with ASP.NET Core 10 MVC, SQL Server and EF Core.

**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything
else enhances that loop.

## Deployment

Production runs as a **Proxmox LXC container**. The `Dockerfile` and `docker-compose.yml` are kept
as a supported path for anyone wanting to self-host, not as the primary deployment target — they
must keep working, but they are not where this instance runs.

## Constraints

- **Compatibility:** No user-facing functionality may be removed or broken — all existing flows must keep working
- **Tech stack:** Stay on ASP.NET Core 10 MVC + SQL Server + EF Core — no framework changes
- **Self-hosting:** The Docker path must remain usable with no additional setup steps
- **Database:** All schema changes require EF Core migrations; auto-applied on startup

Current milestone status, shipped features and known debt live in `.planning/PROJECT.md` — read that
rather than assuming from this file, which describes the shape of the project rather than its
progress.
