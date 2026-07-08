using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.PlayersViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard
{
    [Authorize]
    public class PlayersController(IUserService service) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken token = default)
        {
            var viewModel = new PlayersIndexViewModel
            {
                DungeonMasters = await service.GetAllDungeonMastersAsync(token),
                Players = await service.GetAllPlayersAsync(token)
            };

            return View(viewModel);
        }

    }
}
