// The seeder reaches into every aggregate root, value object,
// and entity type the domain ships. Importing each namespace
// once in this file keeps the per-step files focused on the
// seeding logic instead of drowning in `using` blocks.
global using Cardscape.Domain;
global using Cardscape.Domain.Activities;
global using Cardscape.Domain.Attachments;
global using Cardscape.Domain.Authentication.ExternalLogins;
global using Cardscape.Domain.Authentication.PasswordResets;
global using Cardscape.Domain.Authentication.RevokedTokens;
global using Cardscape.Domain.Authentication.Saml;
global using Cardscape.Domain.Authentication.Scim;
global using Cardscape.Domain.Authentication.Totp;
global using Cardscape.Domain.BackgroundJobs;
global using Cardscape.Domain.Boards;
global using Cardscape.Domain.Cards;
global using Cardscape.Domain.Checklists;
global using Cardscape.Domain.Comments;
global using Cardscape.Domain.Common;
global using Cardscape.Domain.Dashboards;
global using Cardscape.Domain.Idempotency;
global using Cardscape.Domain.Integrations.GitHub;
global using Cardscape.Domain.Integrations.GoogleCalendar;
global using Cardscape.Domain.Integrations.InboundEmail;
global using Cardscape.Domain.Integrations.OAuthApps;
global using Cardscape.Domain.Integrations.Slack;
global using Cardscape.Domain.Labels;
global using Cardscape.Domain.Lists;
global using Cardscape.Domain.Members;
global using Cardscape.Domain.Notifications;
global using Cardscape.Domain.Recurrence;
global using Cardscape.Domain.Security;
global using Cardscape.Domain.UserPreferences;
global using Cardscape.Domain.Voting;
global using Cardscape.Domain.Webhooks;
global using Cardscape.Domain.Workspaces;
