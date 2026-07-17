using GastNyahp.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace GastNyahp.Api.Realtime;

public sealed class FamilyUpdatesHub : Hub
{
    public static string Group(Guid familyId) => $"family:{familyId:N}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext() ?? throw new HubException("No se pudo identificar la conexión.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(http.GetFamilyId()));
        await base.OnConnectedAsync();
    }
}
