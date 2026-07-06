using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Repository.Entities;

public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext activeGroupContext)
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
{
    public DbSet<QuestEntity> Quests { get; set; }

    public DbSet<PlayerSignupEntity> PlayerSignups { get; set; }

    public DbSet<UserEntity> UserEntities { get; set; }

    public DbSet<ShopItemEntity> ShopItems { get; set; }

    public DbSet<UserTransactionEntity> UserTransactions { get; set; }

    public DbSet<TradeItemEntity> TradeItems { get; set; }

    public DbSet<CharacterEntity> Characters { get; set; }

    public DbSet<CharacterImageEntity> CharacterImages { get; set; }

    public DbSet<CharacterClassEntity> CharacterClasses { get; set; }

    public DbSet<DungeonMasterProfileEntity> DungeonMasterProfiles { get; set; }

    public DbSet<DungeonMasterProfileImageEntity> DungeonMasterProfileImages { get; set; }

    public DbSet<ReminderLogEntity> ReminderLogs { get; set; }

    public DbSet<GroupEntity> Groups { get; set; }

    public DbSet<UserGroupEntity> UserGroups { get; set; }

    public DbSet<ContactEntity> Contacts { get; set; }

    public DbSet<ContactImageEntity> ContactImages { get; set; }

    public DbSet<ContactNoteEntity> ContactNotes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure all foreign key relationships to use NO ACTION (Restrict) to avoid cascade cycles
        // This is the safest approach for SQL Server

        modelBuilder.Entity<QuestEntity>()
            .HasOne(q => q.DungeonMaster)
            .WithMany(dm => dm.Quests)
            .HasForeignKey(q => q.DungeonMasterId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<QuestEntity>()
            .HasMany(q => q.ProposedDates)
            .WithOne(pd => pd.Quest)
            .HasForeignKey(pd => pd.QuestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuestEntity>()
            .HasMany(q => q.PlayerSignups)
            .WithOne(ps => ps.Quest)
            .HasForeignKey(ps => ps.QuestId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PlayerSignupEntity>()
            .HasOne(ps => ps.Player)
            .WithMany()
            .HasForeignKey(ps => ps.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerSignupEntity>()
            .HasMany(ps => ps.DateVotes)
            .WithOne(pdv => pdv.PlayerSignup)
            .HasForeignKey(pdv => pdv.PlayerSignupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProposedDateEntity>()
            .HasMany(pd => pd.PlayerVotes)
            .WithOne(pdv => pdv.ProposedDate)
            .HasForeignKey(pdv => pdv.ProposedDateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure unique vote per player per date
        modelBuilder.Entity<PlayerDateVoteEntity>()
            .HasIndex(pdv => new { pdv.PlayerSignupId, pdv.ProposedDateId })
            .IsUnique();

        // Shop entity relationships
        modelBuilder.Entity<ShopItemEntity>()
            .HasOne(si => si.CreatedByDm)
            .WithMany()
            .HasForeignKey(si => si.CreatedByDmId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ShopItemEntity>()
            .HasMany(si => si.Transactions)
            .WithOne(t => t.ShopItem)
            .HasForeignKey(t => t.ShopItemId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserTransactionEntity>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<TradeItemEntity>()
            .HasOne(ti => ti.OfferedByPlayer)
            .WithMany()
            .HasForeignKey(ti => ti.OfferedByPlayerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Character entity relationships
        modelBuilder.Entity<CharacterEntity>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CharacterEntity>()
            .HasMany(c => c.Classes)
            .WithOne(cc => cc.Character)
            .HasForeignKey(cc => cc.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CharacterEntity>()
            .HasOne(c => c.ProfileImage)
            .WithOne(pi => pi.Character)
            .HasForeignKey<CharacterImageEntity>(pi => pi.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CharacterEntity>()
            .HasMany(c => c.PlayerSignups)
            .WithOne(ps => ps.Character)
            .HasForeignKey(ps => ps.CharacterId)
            .OnDelete(DeleteBehavior.NoAction);

        // Contact entity relationships
        modelBuilder.Entity<ContactEntity>()
            .HasMany(c => c.Notes)
            .WithOne(n => n.Contact)
            .HasForeignKey(n => n.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // Author FK uses NoAction to avoid a cascade cycle through UserEntity, same as QuestEntity.DungeonMaster
        modelBuilder.Entity<ContactNoteEntity>()
            .HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // DungeonMasterProfile — Id = UserId (no auto-generation)
        modelBuilder.Entity<DungeonMasterProfileEntity>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        // UserEntity -> DungeonMasterProfileEntity (1:1, Cascade — single path, safe per RESEARCH.md pitfall #1)
        modelBuilder.Entity<DungeonMasterProfileEntity>()
            .HasOne<UserEntity>()
            .WithOne()
            .HasForeignKey<DungeonMasterProfileEntity>(p => p.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // DungeonMasterProfileEntity -> DungeonMasterProfileImageEntity (1:1, Cascade)
        modelBuilder.Entity<DungeonMasterProfileEntity>()
            .HasOne(p => p.ProfileImage)
            .WithOne(pi => pi.DungeonMasterProfile)
            .HasForeignKey<DungeonMasterProfileImageEntity>(pi => pi.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Player signup can optionally have a character
        modelBuilder.Entity<PlayerSignupEntity>()
            .HasOne(ps => ps.Character)
            .WithMany(c => c.PlayerSignups)
            .HasForeignKey(ps => ps.CharacterId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Self-referential follow-up quest relationship
        // One quest may have at most one direct follow-up
        // Delete behaviour: ClientSetNull so deleting a follow-up does not delete the original
        modelBuilder.Entity<QuestEntity>()
            .HasOne(q => q.OriginalQuest)
            .WithOne(q => q.FollowUpQuest)
            .HasForeignKey<QuestEntity>(q => q.OriginalQuestId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        // ReminderLog FK relationships — NoAction to prevent cascade cycles
        modelBuilder.Entity<ReminderLogEntity>()
            .HasOne(r => r.Quest)
            .WithMany()
            .HasForeignKey(r => r.QuestId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReminderLogEntity>()
            .HasOne(r => r.Player)
            .WithMany()
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReminderLogEntity>()
            .HasIndex(r => new { r.QuestId, r.PlayerId })
            .IsUnique();

        // Speeds up session-reminder / finalized-quest sweep queries that filter on both columns together
        modelBuilder.Entity<QuestEntity>()
            .HasIndex(q => new { q.IsFinalized, q.FinalizedDate });

        // Group entity relationships

        // Groups.Name must be unique across the tenant
        modelBuilder.Entity<GroupEntity>()
            .HasIndex(g => g.Name)
            .IsUnique();

        // UserGroups: one membership row per user per group
        modelBuilder.Entity<UserGroupEntity>()
            .HasIndex(ug => new { ug.UserId, ug.GroupId })
            .IsUnique();

        // Quest → Group: NoAction to prevent cascade cycles
        modelBuilder.Entity<QuestEntity>()
            .HasOne(q => q.Group)
            .WithMany()
            .HasForeignKey(q => q.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        // ShopItem → Group: NoAction to prevent cascade cycles
        modelBuilder.Entity<ShopItemEntity>()
            .HasOne(si => si.Group)
            .WithMany()
            .HasForeignKey(si => si.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        // Character → Group: NoAction to prevent cascade cycles
        modelBuilder.Entity<CharacterEntity>()
            .HasOne(c => c.Group)
            .WithMany()
            .HasForeignKey(c => c.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        // Contact → Group: NoAction to prevent cascade cycles
        modelBuilder.Entity<ContactEntity>()
            .HasOne(c => c.Group)
            .WithMany()
            .HasForeignKey(c => c.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        // UserGroup → User: Cascade — removing a user removes their memberships
        modelBuilder.Entity<UserGroupEntity>()
            .HasOne(ug => ug.User)
            .WithMany(u => u.UserGroups)
            .HasForeignKey(ug => ug.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserGroup → Group: Cascade — removing a group removes all memberships
        modelBuilder.Entity<UserGroupEntity>()
            .HasOne(ug => ug.Group)
            .WithMany(g => g.UserGroups)
            .HasForeignKey(ug => ug.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filters for group isolation.
        // QuestEntity and ShopItemEntity carry a GroupId and are the two entities directly scoped
        // to a tenant, so every read is automatically restricted to the caller's active group.
        // A null ActiveGroupId (no group selected yet, or a session that never picked one) must
        // return zero rows, never every group's rows merged together — the caller has to pick a
        // group before any group-scoped data is servable, full stop.
        // Lambda closes over activeGroupContext instance — re-evaluated per query, not at startup
        // CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
        //           That captures the value once (null at model-build time). Always reference the service.
        modelBuilder.Entity<QuestEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId != null &&
                e.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<ShopItemEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId != null &&
                e.GroupId == activeGroupContext.ActiveGroupId);

        // ProposedDateEntity carries no GroupId of its own — it is scoped through its required Quest
        // navigation instead. EF Core's model validation otherwise warns that a required relationship
        // to a filtered entity can behave unexpectedly if the two aren't both filtered, so this mirrors
        // QuestEntity's own fail-closed filter shape rather than relying on every caller reaching this
        // entity only through an already-filtered Quest.
        modelBuilder.Entity<ProposedDateEntity>()
            .HasQueryFilter(pd =>
                activeGroupContext.ActiveGroupId != null &&
                pd.Quest.GroupId == activeGroupContext.ActiveGroupId);

        // PlayerDateVoteEntity carries no GroupId of its own. It has two required FKs
        // (ProposedDateId, PlayerSignupId); either navigation reaches the same quest, so filtering
        // through ProposedDate is sufficient to keep it consistent with both filtered parents above.
        modelBuilder.Entity<PlayerDateVoteEntity>()
            .HasQueryFilter(pdv =>
                activeGroupContext.ActiveGroupId != null &&
                pdv.ProposedDate.Quest.GroupId == activeGroupContext.ActiveGroupId);

        // PlayerSignupEntity carries no GroupId of its own — scoped through its required Quest
        // navigation. This also makes every PlayerSignupRepository method (including the base
        // GetByIdAsync/FindAsync path) automatically group-scoped, not just the ones that manually
        // re-derive the target through an already-filtered quest.PlayerSignups navigation first.
        modelBuilder.Entity<PlayerSignupEntity>()
            .HasQueryFilter(ps =>
                activeGroupContext.ActiveGroupId != null &&
                ps.Quest.GroupId == activeGroupContext.ActiveGroupId);

        // ReminderLogEntity carries no GroupId of its own — scoped through its required Quest
        // navigation, same shape as the other Quest-dependent entities above.
        modelBuilder.Entity<ReminderLogEntity>()
            .HasQueryFilter(r =>
                activeGroupContext.ActiveGroupId != null &&
                r.Quest.GroupId == activeGroupContext.ActiveGroupId);

        // CharacterEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
        // do above. With no active group selected, every character query returns nothing rather than
        // everyone's characters across every group. Do not "fix" this to match the Quest/ShopItem shape —
        // an empty character roster is the intended behavior here, not an oversight.
        modelBuilder.Entity<CharacterEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId != null &&
                e.GroupId == activeGroupContext.ActiveGroupId);

        // CharacterClassEntity and CharacterImageEntity carry no GroupId of their own — both are
        // scoped through their required Character navigation, mirroring CharacterEntity's own filter
        // shape (no SuperAdmin escape hatch) rather than relying on every caller reaching them only
        // through an already-filtered Character.
        modelBuilder.Entity<CharacterClassEntity>()
            .HasQueryFilter(cc =>
                activeGroupContext.ActiveGroupId != null &&
                cc.Character.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<CharacterImageEntity>()
            .HasQueryFilter(ci =>
                activeGroupContext.ActiveGroupId != null &&
                ci.Character.GroupId == activeGroupContext.ActiveGroupId);

        // ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
        // do above — same "per-group roster" shape as CharacterEntity. An empty Contact list when no
        // group is selected is the intended behavior here, not an oversight.
        modelBuilder.Entity<ContactEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId != null &&
                e.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<ContactImageEntity>()
            .HasQueryFilter(ci =>
                activeGroupContext.ActiveGroupId != null &&
                ci.Contact.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<ContactNoteEntity>()
            .HasQueryFilter(cn =>
                activeGroupContext.ActiveGroupId != null &&
                cn.Contact.GroupId == activeGroupContext.ActiveGroupId);

        // UserEntity intentionally excluded — HasQueryFilter on UserEntity breaks ASP.NET Core Identity
        // (login, password reset, and email confirmation all fail silently)
        //
        // UserTransactionEntity carries no GroupId of its own — scoped through its required ShopItem
        // navigation, same fail-closed shape as the Quest-dependent entities above.
        modelBuilder.Entity<UserTransactionEntity>()
            .HasQueryFilter(t =>
                activeGroupContext.ActiveGroupId != null &&
                t.ShopItem.GroupId == activeGroupContext.ActiveGroupId);
    }
}