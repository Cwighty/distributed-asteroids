using Microsoft.AspNetCore.Components;

namespace Asteroids.Client.Components.Pages;
public partial class LobbyPage
{ 
    [Parameter] public long LobbyId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Console.WriteLine("LobbyId: " + LobbyId);
    }
}
