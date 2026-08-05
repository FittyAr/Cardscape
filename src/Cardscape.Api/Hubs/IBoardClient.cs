// IBoardClient and IBoardNotifier moved to the Application
// layer so the static BoardEventBroadcaster in
// Application.Realtime can fan events out without taking
// a dependency on the API project. The SignalR hub and the
// CompositeBoardNotifier implement the Application
// interfaces directly.
namespace Cardscape.Api.Hubs;
