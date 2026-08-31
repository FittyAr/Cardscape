using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardscape.Migrations.PostgreSql.Migrations;

/// <inheritdoc />
public partial class InitialPostgreSql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "activities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activities", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "api_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                HashedSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SecretPrefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Scopes = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RevokedReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RateLimitPerHour = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                BurstSize = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_api_tokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "attachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                MimeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attachments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "background_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_background_jobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "board_automation_rules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Trigger = table.Column<int>(type: "integer", nullable: false),
                TriggerListId = table.Column<Guid>(type: "uuid", nullable: true),
                Action = table.Column<int>(type: "integer", nullable: false),
                ActionArgument = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_automation_rules", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "board_extensions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                ConfigJson = table.Column<string>(type: "text", nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_extensions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "boards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Visibility = table.Column<int>(type: "integer", nullable: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_boards", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "card_aging_settings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Mode = table.Column<int>(type: "integer", nullable: false),
                StaleAfterDays = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_aging_settings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "card_mirrors",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SourceCardId = table.Column<Guid>(type: "uuid", nullable: false),
                MirroredCardId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetListId = table.Column<Guid>(type: "uuid", nullable: false),
                MirroredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                MirroredBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_mirrors", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "card_recurrences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                IntervalDays = table.Column<int>(type: "integer", nullable: false),
                NextOccurrenceAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_recurrences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "card_snoozes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SnoozedBy = table.Column<Guid>(type: "uuid", nullable: false),
                SnoozedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_snoozes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "card_votes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                VotedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_votes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "cards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ListId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                Position = table.Column<double>(type: "double precision", nullable: false),
                DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                CoverColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cards", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "checklists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checklists", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "comments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_comments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "custom_field_definitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                OptionsJson = table.Column<string>(type: "text", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_custom_field_definitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "custom_field_values",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                ValueJson = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_custom_field_values", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "dashcards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ConfigurationJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                Position = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dashcards", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "external_logins",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<int>(type: "integer", nullable: false),
                Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_external_logins", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "github_pull_request_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                RepoFullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PullRequestNumber = table.Column<int>(type: "integer", nullable: false),
                PullRequestUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_github_pull_request_links", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "github_repo_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                RepoFullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Events = table.Column<string>(type: "text", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_github_repo_links", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "google_calendar_connections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                GoogleEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                EncryptedRefreshToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                CalendarId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                EventMappingsJson = table.Column<string>(type: "text", nullable: false),
                LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastSyncErrorAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastSyncError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_google_calendar_connections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "idempotency_keys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ResponseStatusCode = table.Column<int>(type: "integer", nullable: false, defaultValue: 200),
                ResponseJson = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotency_keys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "inbound_email_addresses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                EmailAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                TargetListId = table.Column<Guid>(type: "uuid", nullable: false),
                Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbound_email_addresses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "labels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_labels", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "lists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Position = table.Column<double>(type: "double precision", nullable: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                MaxCardsSoft = table.Column<int>(type: "integer", nullable: true),
                MaxCardsHard = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lists", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                IsRead = table.Column<bool>(type: "boolean", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "oauth_access_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AppId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Scopes = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_oauth_access_tokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "oauth_apps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ClientSecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                AllowedScopes = table.Column<string>(type: "text", nullable: false),
                RedirectUris = table.Column<string>(type: "text", nullable: false),
                IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_oauth_apps", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "oauth_authorization_codes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AppId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RedirectUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Scopes = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_oauth_authorization_codes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "password_resets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RequestedFromIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_password_resets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "revoked_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_revoked_tokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "saml_connections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IdpEntityId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                IdpMetadataUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                IdpMetadataXml = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                SpEntityId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_saml_connections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "scim_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                TokenPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scim_tokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "slack_channels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SlackWorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                ChannelId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ChannelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Events = table.Column<string>(type: "text", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_slack_channels", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "slack_workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                TeamId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProtectedBotToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_slack_workspaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "totp_credentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                EncryptedSecret = table.Column<string>(type: "text", nullable: false),
                RecoveryCodesHash = table.Column<string>(type: "text", nullable: false),
                LastUsedCounter = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_totp_credentials", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "user_preferences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ThemeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "default"),
                Mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_preferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                AvatarUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsAnonymised = table.Column<bool>(type: "boolean", nullable: false),
                AnonymisedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsRestricted = table.Column<bool>(type: "boolean", nullable: false),
                RestrictedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "webhook_deliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_webhook_deliveries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "webhook_endpoints",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ProtectedSecret = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Events = table.Column<string>(type: "text", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_webhook_endpoints", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "workspace_invitations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                InvitedBy = table.Column<Guid>(type: "uuid", nullable: false),
                InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TokenPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AcceptedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_invitations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                Region = table.Column<int>(type: "integer", nullable: false),
                RequireTwoFactor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "board_members",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_members", x => x.Id);
                table.ForeignKey(
                    name: "FK_board_members_boards_BoardId",
                    column: x => x.BoardId,
                    principalTable: "boards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_stars",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                StarredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_stars", x => x.Id);
                table.ForeignKey(
                    name: "FK_board_stars_boards_BoardId",
                    column: x => x.BoardId,
                    principalTable: "boards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "card_labels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_labels", x => x.Id);
                table.ForeignKey(
                    name: "FK_card_labels_cards_CardId",
                    column: x => x.CardId,
                    principalTable: "cards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "card_members",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_card_members", x => x.Id);
                table.ForeignKey(
                    name: "FK_card_members_cards_CardId",
                    column: x => x.CardId,
                    principalTable: "cards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "checklist_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                Position = table.Column<double>(type: "double precision", nullable: false),
                AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checklist_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_checklist_items_checklists_ChecklistId",
                    column: x => x.ChecklistId,
                    principalTable: "checklists",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "workspace_members",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_members", x => x.Id);
                table.ForeignKey(
                    name: "FK_workspace_members_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_activities_ActorId_OccurredAt",
            table: "activities",
            columns: new[] { "ActorId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_activities_BoardId_OccurredAt_Id",
            table: "activities",
            columns: new[] { "BoardId", "OccurredAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_activities_CardId_OccurredAt_Id",
            table: "activities",
            columns: new[] { "CardId", "OccurredAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_api_tokens_HashedSecret",
            table: "api_tokens",
            column: "HashedSecret",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_api_tokens_UserId_CreatedAt",
            table: "api_tokens",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_attachments_CardId_IsDeleted_CreatedAt",
            table: "attachments",
            columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_attachments_UploaderId",
            table: "attachments",
            column: "UploaderId");

        migrationBuilder.CreateIndex(
            name: "IX_background_jobs_Status_CompletedAt",
            table: "background_jobs",
            columns: new[] { "Status", "CompletedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_background_jobs_Status_ScheduledFor",
            table: "background_jobs",
            columns: new[] { "Status", "ScheduledFor" });

        migrationBuilder.CreateIndex(
            name: "IX_board_automation_rules_BoardId_IsEnabled_Position",
            table: "board_automation_rules",
            columns: new[] { "BoardId", "IsEnabled", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_board_automation_rules_BoardId_Position",
            table: "board_automation_rules",
            columns: new[] { "BoardId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_board_extensions_BoardId_Kind",
            table: "board_extensions",
            columns: new[] { "BoardId", "Kind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_members_BoardId_UserId",
            table: "board_members",
            columns: new[] { "BoardId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_stars_BoardId_UserId",
            table: "board_stars",
            columns: new[] { "BoardId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_boards_WorkspaceId",
            table: "boards",
            column: "WorkspaceId");

        migrationBuilder.CreateIndex(
            name: "IX_card_labels_CardId_LabelId",
            table: "card_labels",
            columns: new[] { "CardId", "LabelId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_card_members_CardId_UserId",
            table: "card_members",
            columns: new[] { "CardId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_card_mirrors_MirroredCardId",
            table: "card_mirrors",
            column: "MirroredCardId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_card_mirrors_SourceCardId",
            table: "card_mirrors",
            column: "SourceCardId");

        migrationBuilder.CreateIndex(
            name: "IX_card_recurrences_CardId",
            table: "card_recurrences",
            column: "CardId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_card_votes_CardId",
            table: "card_votes",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_card_votes_CardId_UserId",
            table: "card_votes",
            columns: new[] { "CardId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_cards_ListId",
            table: "cards",
            column: "ListId");

        migrationBuilder.CreateIndex(
            name: "IX_cards_ListId_Position",
            table: "cards",
            columns: new[] { "ListId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_checklist_items_ChecklistId_Position",
            table: "checklist_items",
            columns: new[] { "ChecklistId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_checklists_CardId_IsDeleted_CreatedAt",
            table: "checklists",
            columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_comments_AuthorId",
            table: "comments",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_comments_CardId_IsDeleted_CreatedAt",
            table: "comments",
            columns: new[] { "CardId", "IsDeleted", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_custom_field_definitions_BoardId_Position",
            table: "custom_field_definitions",
            columns: new[] { "BoardId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_custom_field_values_CardId",
            table: "custom_field_values",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_custom_field_values_Field_Card",
            table: "custom_field_values",
            columns: new[] { "FieldDefinitionId", "CardId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_dashcards_BoardId",
            table: "dashcards",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_Provider_Subject",
            table: "external_logins",
            columns: new[] { "Provider", "Subject" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_UserId_LastUsedAt",
            table: "external_logins",
            columns: new[] { "UserId", "LastUsedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_github_pull_request_links_CardId",
            table: "github_pull_request_links",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_github_repo_links_BoardId",
            table: "github_repo_links",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_github_repo_links_BoardId_RepoFullName",
            table: "github_repo_links",
            columns: new[] { "BoardId", "RepoFullName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_google_calendar_connections_UserId",
            table: "google_calendar_connections",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_google_calendar_connections_WorkspaceId",
            table: "google_calendar_connections",
            column: "WorkspaceId");

        migrationBuilder.CreateIndex(
            name: "IX_idempotency_keys_ExpiresAt",
            table: "idempotency_keys",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_idempotency_keys_OwnerId",
            table: "idempotency_keys",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_idempotency_keys_OwnerId_Key",
            table: "idempotency_keys",
            columns: new[] { "OwnerId", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbound_email_addresses_EmailAddress",
            table: "inbound_email_addresses",
            column: "EmailAddress",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbound_email_addresses_WorkspaceId",
            table: "inbound_email_addresses",
            column: "WorkspaceId");

        migrationBuilder.CreateIndex(
            name: "IX_labels_BoardId_IsDeleted_Name",
            table: "labels",
            columns: new[] { "BoardId", "IsDeleted", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_lists_BoardId",
            table: "lists",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_lists_BoardId_Position",
            table: "lists",
            columns: new[] { "BoardId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_UserId_CreatedAt",
            table: "notifications",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_UserId_IsRead_CreatedAt",
            table: "notifications",
            columns: new[] { "UserId", "IsRead", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_oauth_access_tokens_AppId",
            table: "oauth_access_tokens",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_access_tokens_ExpiresAt",
            table: "oauth_access_tokens",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_access_tokens_RevokedAt",
            table: "oauth_access_tokens",
            column: "RevokedAt");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_access_tokens_TokenHash",
            table: "oauth_access_tokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_oauth_access_tokens_UserId_CreatedAt",
            table: "oauth_access_tokens",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_oauth_apps_ClientId",
            table: "oauth_apps",
            column: "ClientId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_oauth_apps_Name",
            table: "oauth_apps",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_apps_OwnerId",
            table: "oauth_apps",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_authorization_codes_AppId",
            table: "oauth_authorization_codes",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_oauth_authorization_codes_CodeHash",
            table: "oauth_authorization_codes",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_oauth_authorization_codes_UserId",
            table: "oauth_authorization_codes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_password_resets_TokenHash",
            table: "password_resets",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_password_resets_UserId",
            table: "password_resets",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_revoked_tokens_Jti",
            table: "revoked_tokens",
            column: "Jti",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_revoked_tokens_TokenExpiresAt",
            table: "revoked_tokens",
            column: "TokenExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_revoked_tokens_UserId",
            table: "revoked_tokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_saml_connections_Slug",
            table: "saml_connections",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_saml_connections_WorkspaceId",
            table: "saml_connections",
            column: "WorkspaceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_slack_channels_BoardId",
            table: "slack_channels",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_slack_channels_SlackWorkspaceId",
            table: "slack_channels",
            column: "SlackWorkspaceId");

        migrationBuilder.CreateIndex(
            name: "IX_slack_channels_WorkspaceId_BoardId_ChannelId",
            table: "slack_channels",
            columns: new[] { "SlackWorkspaceId", "BoardId", "ChannelId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_slack_workspaces_WorkspaceId",
            table: "slack_workspaces",
            column: "WorkspaceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_totp_credentials_UserId",
            table: "totp_credentials",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_Email",
            table: "users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_deliveries_EndpointId",
            table: "webhook_deliveries",
            column: "EndpointId");

        migrationBuilder.CreateIndex(
            name: "IX_webhook_deliveries_EndpointId_CreatedAt",
            table: "webhook_deliveries",
            columns: new[] { "EndpointId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_webhook_deliveries_EventType",
            table: "webhook_deliveries",
            column: "EventType");

        migrationBuilder.CreateIndex(
            name: "IX_webhook_endpoints_BoardId",
            table: "webhook_endpoints",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_invitations_Email_AcceptedAt_RevokedAt_InvitedAt",
            table: "workspace_invitations",
            columns: new[] { "Email", "AcceptedAt", "RevokedAt", "InvitedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_invitations_TokenHash",
            table: "workspace_invitations",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_invitations_WorkspaceId_InvitedAt",
            table: "workspace_invitations",
            columns: new[] { "WorkspaceId", "InvitedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_members_WorkspaceId_UserId",
            table: "workspace_members",
            columns: new[] { "WorkspaceId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspaces_Region",
            table: "workspaces",
            column: "Region");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "activities");

        migrationBuilder.DropTable(
            name: "api_tokens");

        migrationBuilder.DropTable(
            name: "attachments");

        migrationBuilder.DropTable(
            name: "background_jobs");

        migrationBuilder.DropTable(
            name: "board_automation_rules");

        migrationBuilder.DropTable(
            name: "board_extensions");

        migrationBuilder.DropTable(
            name: "board_members");

        migrationBuilder.DropTable(
            name: "board_stars");

        migrationBuilder.DropTable(
            name: "card_aging_settings");

        migrationBuilder.DropTable(
            name: "card_labels");

        migrationBuilder.DropTable(
            name: "card_members");

        migrationBuilder.DropTable(
            name: "card_mirrors");

        migrationBuilder.DropTable(
            name: "card_recurrences");

        migrationBuilder.DropTable(
            name: "card_snoozes");

        migrationBuilder.DropTable(
            name: "card_votes");

        migrationBuilder.DropTable(
            name: "checklist_items");

        migrationBuilder.DropTable(
            name: "comments");

        migrationBuilder.DropTable(
            name: "custom_field_definitions");

        migrationBuilder.DropTable(
            name: "custom_field_values");

        migrationBuilder.DropTable(
            name: "dashcards");

        migrationBuilder.DropTable(
            name: "external_logins");

        migrationBuilder.DropTable(
            name: "github_pull_request_links");

        migrationBuilder.DropTable(
            name: "github_repo_links");

        migrationBuilder.DropTable(
            name: "google_calendar_connections");

        migrationBuilder.DropTable(
            name: "idempotency_keys");

        migrationBuilder.DropTable(
            name: "inbound_email_addresses");

        migrationBuilder.DropTable(
            name: "labels");

        migrationBuilder.DropTable(
            name: "lists");

        migrationBuilder.DropTable(
            name: "notifications");

        migrationBuilder.DropTable(
            name: "oauth_access_tokens");

        migrationBuilder.DropTable(
            name: "oauth_apps");

        migrationBuilder.DropTable(
            name: "oauth_authorization_codes");

        migrationBuilder.DropTable(
            name: "password_resets");

        migrationBuilder.DropTable(
            name: "revoked_tokens");

        migrationBuilder.DropTable(
            name: "saml_connections");

        migrationBuilder.DropTable(
            name: "scim_tokens");

        migrationBuilder.DropTable(
            name: "slack_channels");

        migrationBuilder.DropTable(
            name: "slack_workspaces");

        migrationBuilder.DropTable(
            name: "totp_credentials");

        migrationBuilder.DropTable(
            name: "user_preferences");

        migrationBuilder.DropTable(
            name: "users");

        migrationBuilder.DropTable(
            name: "webhook_deliveries");

        migrationBuilder.DropTable(
            name: "webhook_endpoints");

        migrationBuilder.DropTable(
            name: "workspace_invitations");

        migrationBuilder.DropTable(
            name: "workspace_members");

        migrationBuilder.DropTable(
            name: "boards");

        migrationBuilder.DropTable(
            name: "cards");

        migrationBuilder.DropTable(
            name: "checklists");

        migrationBuilder.DropTable(
            name: "workspaces");
    }
}
