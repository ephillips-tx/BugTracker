#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Extensions;
using BugTracker.Services.Interfaces;
using BugTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using BugTracker.Data.Migrations;
using System.Text.Encodings.Web;
using System.ComponentModel;
using System.Web;
using BugTracker.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace BugTracker.Controllers
{
    public class InvitesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBTProjectService _projectService;
        private readonly IBTInviteService _inviteService;
        private readonly IEmailSender _emailService;
        private readonly UserManager<BTUser> _userManager;
        private readonly IBTCompanyInfoService _companyInfoService;

        public InvitesController(ApplicationDbContext context,
                                 IBTInviteService inviteService,
                                 IEmailSender emailService,
                                 UserManager<BTUser> userManager,
                                 IBTProjectService projectService,
                                 IBTCompanyInfoService companyInfoService)
        {
            _context = context;
            _inviteService = inviteService;
            _emailService = emailService;
            _userManager = userManager;
            _projectService = projectService;
            _companyInfoService = companyInfoService;
        }

        // GET: Invites
        public async Task<IActionResult> Index()
        {
            int companyId = User.Identity.GetCompanyId().Value;

            List<Invite> invites = await _inviteService.GetAllInvitesByCompanyAsync(companyId);

            return View(invites);
        }

        // GET: Invites/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Invites == null)
            {
                return NotFound();
            }

            var invite = await _context.Invites
                .Include(i => i.Company)
                .Include(i => i.Invitee)
                .Include(i => i.Invitor)
                .Include(i => i.Project)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (invite == null)
            {
                return NotFound();
            }

            return View(invite);
        }

        // GET: Invites/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            ViewData["CurrentPath"] = "Invites / New Invite";

            ViewData["ProjectId"] = new SelectList(await _projectService.GetAllProjectsByCompanyAsync(companyId), "Id", "Name");

            return View();
        }

        // POST: Invites/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ProjectId,InviteeEmail,InviteeFirstName,InviteeLastName,Message")] Invite invite)
        {
            int companyId = User.Identity.GetCompanyId().Value;
            BTUser btUser = await _userManager.GetUserAsync(User);
            string returnUrl = null;

            if (ModelState.IsValid)
            {
                try
                {
                    // Fill in other invite info, join date, companyId, etc. 
                    invite.Company = await _companyInfoService.GetCompanyInfoByIdAsync(companyId);
                    invite.CompanyId = invite.Company.Id;
                    invite.CompanyToken = Guid.NewGuid();
                    invite.InviteDate = DateTime.Today;
                    invite.Invitor = btUser;
                    invite.InvitorId = btUser.Id;
                    invite.Message = HttpUtility.HtmlDecode(invite.Message);
                    invite.Project = await _projectService.GetProjectByIdAsync(invite.ProjectId, companyId);

                    // Add invite to DB
                    await _inviteService.AddNewInviteAsync(invite);

                    // Build 
                    returnUrl ??= Url.Content("~/");
                    var userId = btUser.Id;

                    var encodedCompanyId = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(invite.CompanyId.ToString())); // decode in ProcessInvite
                    //var encodedCompanyId = invite.CompanyId;

                    var callbackUrl = Url.Action("ProcessInvite","Invites",
                        values: new { token = invite.CompanyToken, email = invite.InviteeEmail, company = encodedCompanyId },
                        protocol: Request.Scheme);

                    string subject = $"BugTracker: Invite to {invite.Company.Name}";
                    invite.Message += $"\n  <br>\n  <br> Click the link below to join our team on BugTracker.\n  <br> \n  <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Join {btUser.Company.Name}</a>";
                    invite.Message += $"\n  <br>\n  <br> Thank you,\n  <br> {invite.Invitor.FullName} \n";

                    await _emailService.SendEmailAsync(invite.InviteeEmail, subject, invite.Message);

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("*************> ERROR CREATING INVITE <**************");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.InnerException.Message);
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine(ex.InnerException.InnerException.Message);
                    }
                    Console.WriteLine("****************************************************");
                    throw;
                }
                
            }

            return View(invite);
        }

        // GET: Invites/ProcessInvite/5
        [HttpGet]
        public async Task<IActionResult> ProcessInvite(Guid token, string email, string company)
        {
            ViewData["CurrentPath"] = "Invite / Accept Invitation";

            if (email == null)
            {
                Console.WriteLine("***  route values are null?  ***");
                return NotFound();
            }

            int companyId = Convert.ToInt32(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(company)));
            bool result = await _inviteService.AnyInviteAsync(token, email, companyId);
            if (!result)
            {
                Console.WriteLine("*********> No Invite Found <*********");
                Console.WriteLine(token);
                Console.WriteLine(email);
                Console.WriteLine(company);
                Console.WriteLine("*************************************");

                return NotFound();
            }

            try
            {
                Invite invite = await _inviteService.GetInviteAsync(token, email, companyId);
                invite.Message = HttpUtility.HtmlDecode(invite.Message);

                //Console.WriteLine("<<<<<<<<<<<< INVITE OBJ >>>>>>>>>>>>>");
                //foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(invite))
                //{
                //    string name = descriptor.Name;
                //    object value = descriptor.GetValue(invite);
                //    Console.WriteLine($"{name}: {value}");
                //}
                //Console.WriteLine("***************************");

                if (invite == null)
                {
                    Console.WriteLine("invite is null");
                    return RedirectToAction("Index");
                }

                return View(invite);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("&&&&&&&&&&");
                Console.WriteLine(ex.InnerException);
                throw;
            }
        }

        // GET: Invites/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["CurrentPath"] = "Invites / Edit Invite";
            if (id == null)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 
            {
                return NotFound();
            }

            int companyId = User.Identity.GetCompanyId().Value;

            int inviteId = id.GetValueOrDefault(-1);
            Invite invite = await _inviteService.GetInviteAsync(inviteId, companyId);
            ViewData["ProjectId"] = new SelectList(await _projectService.GetAllProjectsByCompanyAsync(companyId), "Id", "Name");

            if (invite == null)
            {
                return NotFound();
            }

            return View(invite);
        }

        // POST: Invites/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,InviteDate,JoinDate,CompanyToken,CompanyId,ProjectId,InvitorId,InviteeId,InviteeEmail,InviteeFirstName,InviteeLastName,IsValid")] Invite invite)
        {
            if (id != invite.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(invite);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InviteExists(invite.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", invite.CompanyId);
            ViewData["InviteeId"] = new SelectList(_context.Users, "Id", "Id", invite.InviteeId);
            ViewData["InvitorId"] = new SelectList(_context.Users, "Id", "Id", invite.InvitorId);
            ViewData["ProjectId"] = new SelectList(_context.Projects, "Id", "Name", invite.ProjectId);
            return View(invite);
        }

        // GET: Invites/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Invites == null)
            {
                return NotFound();
            }

            var invite = await _context.Invites
                .Include(i => i.Company)
                .Include(i => i.Invitee)
                .Include(i => i.Invitor)
                .Include(i => i.Project)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (invite == null)
            {
                return NotFound();
            }

            return View(invite);
        }

        // POST: Invites/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Invites == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Invites'  is null.");
            }
            var invite = await _context.Invites.FindAsync(id);
            if (invite != null)
            {
                _context.Invites.Remove(invite);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InviteExists(int id)
        {
          return (_context.Invites?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
