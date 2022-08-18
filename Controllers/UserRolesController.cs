using BugTracker.Extensions;
using BugTracker.Models;
using BugTracker.Models.ViewModels;
using BugTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BugTracker.Controllers
{
    [Authorize]
    public class UserRolesController : Controller
    {
        private readonly IBTRolesService _rolesService;
        private readonly IBTCompanyInfoService _companyInfoService;

        public UserRolesController(IBTRolesService rolesService,
                                   IBTCompanyInfoService companyInfoService)
        {
            _rolesService = rolesService;
            _companyInfoService = companyInfoService;
        }

        [HttpGet]
        public async Task<IActionResult> ManageUserRoles()
        {
            // add instance of viewmodel as a list (viewmodel)
            List<ManageUserRolesViewModel> model = new();

            // get companyId - user claims
            int companyId = User.Identity.GetCompanyId().Value;

            // get all company users
            List<BTUser> users = await _companyInfoService.GetAllMembersAsync(companyId);

            // loop over users to populate ViewModel
            //  - instantiate ViewModel
            //  - use _rolesService
            //  - Create multi-selects
            foreach (BTUser user in users)
            {
                ManageUserRolesViewModel viewmodel = new();
                viewmodel.BTUser = user;
                IEnumerable<string> selected = await _rolesService.GetUserRolesAsync(user);
                viewmodel.Roles = new MultiSelectList(await _rolesService.GetRolesAsync(), "Name", "Name", selected);

                model.Add(viewmodel);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageUserRoles(ManageUserRolesViewModel member)
        {
            // get Company Id
            int companyId = User.Identity.GetCompanyId().Value;

            // instantiate BTUser
            BTUser btUser = (await _companyInfoService.GetAllMembersAsync(companyId)).FirstOrDefault(u => u.Id == member.BTUser.Id);

            // Get Roles for the User
            IEnumerable<string> roles = await _rolesService.GetUserRolesAsync(btUser);

            // grab selected role
            string userRole = member.SelectedRoles.FirstOrDefault();

            
            if (!string.IsNullOrEmpty(userRole))
            {
                // Remove the user from their roles
                if(await _rolesService.RemoveUserFromRolesAsync(btUser, roles))
                {
                    // Add user to new role
                    await _rolesService.AddUserToRoleAsync(btUser, userRole);
                }
            }
            // navigate back to view
            return RedirectToAction(nameof(ManageUserRoles));
        }
    }
}
