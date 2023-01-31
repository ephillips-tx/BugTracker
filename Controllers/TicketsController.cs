using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BugTracker.Data;
using BugTracker.Models;
using Microsoft.AspNetCore.Identity;
using BugTracker.Extensions;
using BugTracker.Models.Enums;
using BugTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using BugTracker.Models.ViewModels;
using System.Web;

namespace BugTracker.Controllers
{
    public class TicketsController : Controller
    {
        private readonly UserManager<BTUser> _userManager;
        private readonly IBTProjectService _projectService;
        private readonly IBTLookupService _lookupService;
        private readonly IBTTicketService _ticketService;
        private readonly IBTFileService _fileService;
        private readonly IBTTicketHistoryService _historyService;

        public TicketsController(UserManager<BTUser> userManager,
                                 IBTProjectService projectService,
                                 IBTLookupService lookupService,
                                 IBTTicketService ticketService,
                                 IBTFileService fileService,
                                 IBTTicketHistoryService historyService)
        {
            _userManager = userManager;
            _projectService = projectService;
            _lookupService = lookupService;
            _ticketService = ticketService;
            _fileService = fileService;
            _historyService = historyService;
        }

        public async Task<IActionResult> MyTickets()
        {
            BTUser btUser = await _userManager.GetUserAsync(User);
            List<Ticket> tickets = await _ticketService.GetTicketsByUserIdAsync(btUser.Id, btUser.CompanyId);

            ViewData["CurrentPath"] = "My Tickets List";

            if (User.IsInRole(nameof(Roles.Developer)) || User.IsInRole(nameof(Roles.Submitter)))
            {
                return View(tickets.Where(t => t.Archived == false));
            }

            return View(tickets);
        }

        public async Task<IActionResult> AllTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);

            ViewData["CurrentPath"] = "All Tickets List";

            if (User.IsInRole(nameof(Roles.Developer)) || User.IsInRole(nameof(Roles.Submitter)))
            {
                return View(tickets.Where(t => t.Archived == false));
            }

            return View(tickets);
        }

        public async Task<IActionResult> ArchivedTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            List<Ticket> tickets = await _ticketService.GetArchivedTicketsAsync(companyId);

            return View(tickets);
        }

        [Authorize(Roles="Admin,ProjectManager")]
        public async Task<IActionResult> UnassignedTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            string btUserId = _userManager.GetUserId(User);

            ViewData["CurrentPath"] = "Assign Tickets";

            //List<Ticket> tickets = await _ticketService.GetUnassignedTicketsAsync(companyId);
            List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);

            if (User.IsInRole(nameof(Roles.Admin)))
            {
                return View(tickets);
            }
            else
            {
                List<Ticket> pmTickets = new();

                foreach(Ticket ticket in tickets)
                {
                    if(await _projectService.IsAssignedProjectManagerAsync(btUserId, ticket.ProjectId))
                    {
                        pmTickets.Add(ticket);
                    }
                }
                return View(pmTickets);
            }
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpGet]
        public async Task<IActionResult> AssignDeveloper(int id)
        {
            AssignDeveloperViewModel model = new();

            ViewData["CurrentPath"] = "Assign Tickets / Assign Developer";

            model.Ticket = await _ticketService.GetTicketByIdAsync(id);
            model.Developers = new SelectList(await _projectService.GetProjectMembersByRoleAsync(model.Ticket.ProjectId,nameof(Roles.Developer)),
                                                                                                "Id","FullName");
            return View(model);
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDeveloper(AssignDeveloperViewModel model)
        {
            if(model.DeveloperId != null)
            {
                BTUser btUser = await _userManager.GetUserAsync(User);
                Ticket oldTicket = await _ticketService.GetTicketAsNoTrackingAsync(model.Ticket.Id);
                try
                {
                    await _ticketService.AssignTicketAsync(model.Ticket.Id, model.DeveloperId);

                }
                catch (Exception)
                {
                    throw;
                }
                // new ticket
                Ticket newTicket = await _ticketService.GetTicketAsNoTrackingAsync(model.Ticket.Id);
                await _historyService.AddHistoryAsync(oldTicket, newTicket, btUser.Id);

                return RedirectToAction(nameof(Details), new { id = model.Ticket.Id });
            }

            return RedirectToAction(nameof(AssignDeveloper), new { id = model.Ticket.Id });
        }

        // GET: Tickets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Ticket ticket = await _ticketService.GetTicketByIdAsync(id.Value);
            foreach(var h in ticket.History)
            {
                h.User = await _userManager.FindByIdAsync(h.UserId);
            }
            ticket.Description = HttpUtility.HtmlDecode(ticket.Description);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewData["CurrentPath"] = "Tickets / Details";

            return View(ticket);
        }

        // GET: Tickets/Create
        public async Task<IActionResult> Create()
        {
            BTUser btUser = await _userManager.GetUserAsync(User);
            int companyId = User.Identity.GetCompanyId().Value;
            ViewData["CurrentPath"] = "Tickets / Create";

            if (User.IsInRole(nameof(Roles.Admin)))
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetAllProjectsByCompanyAsync(companyId), "Id", "Name");
            }
            else
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetUserProjectsAsync(btUser.Id), "Id", "Name");
            }

            ViewData["TicketPriorityId"] = new SelectList(await _lookupService.GetTicketPrioritiesAsync(), "Id", "Name");
            ViewData["TicketTypeId"] = new SelectList(await _lookupService.GetTicketTypesAsync(), "Id", "Name");

            return View();
        }

        // POST: Tickets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,ProjectId,TicketTypeId,TicketPriorityId")] Ticket ticket)
        {
            BTUser btUser = await _userManager.GetUserAsync(User);

            if (ModelState.IsValid)
            {
                try
                {
                    ticket.Created = DateTime.Now;
                    ticket.OwnerUserId = btUser.Id;
                    ticket.TicketStatusId = (await _ticketService.LookupTicketStatusIdAsync(nameof(BTTicketStatus.New))).Value;

                    await _ticketService.AddNewTicketAsync(ticket);

                    Ticket newTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);
                    await _historyService.AddHistoryAsync(null, newTicket, btUser.Id);

                    //TODO: add Ticket Notification
                }
                catch (Exception)
                {
                    throw;
                }

                return RedirectToAction(nameof(MyTickets));
            }

            if (User.IsInRole(nameof(Roles.Admin)))
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetAllProjectsByCompanyAsync(btUser.CompanyId), "Id", "Name");
            }
            else
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetUserProjectsAsync(btUser.Id), "Id", "Name");
            }
            ViewData["TicketPriorityId"] = new SelectList(await _lookupService.GetTicketPrioritiesAsync(), "Id", "Name");
            ViewData["TicketTypeId"] = new SelectList(await _lookupService.GetTicketTypesAsync(), "Id", "Name");

            return View(ticket);
        }

        // GET: Tickets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Ticket ticket = await _ticketService.GetTicketByIdAsync(id.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewData["TicketPriorityId"] = new SelectList(await _lookupService.GetTicketPrioritiesAsync(), "Id", "Name", ticket.TicketPriorityId);
            ViewData["TicketStatusId"] = new SelectList(await _lookupService.GetTicketStatusesAsync(), "Id", "Name", ticket.TicketStatusId);
            ViewData["TicketTypeId"] = new SelectList(await _lookupService.GetTicketTypesAsync(), "Id", "Name", ticket.TicketTypeId);

            return View(ticket);
        }

        // POST: Tickets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Created,Updated,Archived,ProjectId,TicketTypeId,TicketPriorityId,TicketStatusId,OwnerUserId,DeveloperUserId")] Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                BTUser btUser = await _userManager.GetUserAsync(User);
                Ticket oldTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);

                try
                {
                    ticket.Created = ticket.Created.ToUniversalTime();
                    ticket.Updated = DateTime.UtcNow;
                    await _ticketService.UpdateTicketAsync(ticket);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await TicketExists(ticket.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw; // throw error given from DB onto the stack
                    }
                }
                
                Ticket newTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);
                await _historyService.AddHistoryAsync(oldTicket, newTicket, btUser.Id);

                return RedirectToAction(nameof(MyTickets));
            }

            ViewData["TicketPriorityId"] = new SelectList(await _lookupService.GetTicketPrioritiesAsync(), "Id", "Name", ticket.TicketPriorityId);
            ViewData["TicketStatusId"] = new SelectList(await _lookupService.GetTicketStatusesAsync(), "Id", "Name", ticket.TicketStatusId);
            ViewData["TicketTypeId"] = new SelectList(await _lookupService.GetTicketTypesAsync(), "Id", "Name", ticket.TicketTypeId);

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTicketComment([Bind("Id,TicketId,Comment")] TicketComment ticketComment)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    ticketComment.UserId = _userManager.GetUserId(User);
                    ticketComment.Created = DateTime.Now;
                    ticketComment.Ticket = await _ticketService.GetTicketByIdAsync(ticketComment.TicketId);

                    await _ticketService.AddTicketCommentAsync(ticketComment);

                    //Add history
                    await _historyService.AddHistoryAsync(ticketComment.TicketId,nameof(TicketComment), ticketComment.UserId);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Details), new {id = ticketComment.TicketId});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTicketAttachment([Bind("Id,FormFile,Description,TicketId")] TicketAttachment ticketAttachment)
        {
            string statusMessage;

            if(ModelState.IsValid && ticketAttachment.FormFile != null)
            {
                try
                {
                    ticketAttachment.FileData = await _fileService.ConvertFileToByteArrayAsync(ticketAttachment.FormFile);
                    ticketAttachment.FileName = ticketAttachment.FormFile.FileName;
                    ticketAttachment.FileContentType = ticketAttachment.FormFile.ContentType;

                    ticketAttachment.Created = DateTime.UtcNow;
                    ticketAttachment.UserId = _userManager.GetUserId(User);

                    await _ticketService.AddTicketAttachmentAsync(ticketAttachment);

                    await _historyService.AddHistoryAsync(ticketAttachment.TicketId, nameof(TicketAttachment), ticketAttachment.UserId);

                }
                catch (Exception)
                {
                    throw;
                }
                statusMessage = "Success: New attachment added to ticket.";
            }
            else
            {
                statusMessage = "Error: invalid data.";
            }

            return RedirectToAction(nameof(Details),new { id = ticketAttachment.TicketId, message = statusMessage});
        }

        // GET: Tickets/Archive/5
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Ticket ticket = await _ticketService.GetTicketByIdAsync(id.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // POST: Tickets/Archive/5
        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            Ticket ticket = await _ticketService.GetTicketByIdAsync(id);
            ticket.Archived = true;
            await _ticketService.UpdateTicketAsync(ticket);

            return RedirectToAction(nameof(MyTickets));
        }

        // GET: Tickets/Restore/5
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Restore(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Ticket ticket = await _ticketService.GetTicketByIdAsync(id.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // POST: Tickets/Archive/5
        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ActionName("Restore")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreConfirmed(int id)
        {
            Ticket ticket = await _ticketService.GetTicketByIdAsync(id);
            ticket.Archived = false;
            await _ticketService.UpdateTicketAsync(ticket);

            return RedirectToAction(nameof(MyTickets));
        }

        public async Task<IActionResult> ShowFile(int id)
        {
            TicketAttachment ticketAttachment = await _ticketService.GetTicketAttachmentByIdAsync(id);
            string fileName = ticketAttachment.FileName;
            byte[] fileData = ticketAttachment.FileData;
            string ext = Path.GetExtension(fileName).Replace(".", "");

            Response.Headers.Add("Content-Disposition", $"inline; filename={fileName}");
            return File(fileData, $"application/{ext}");
        }

        private async Task<bool> TicketExists(int id)
        {
            int companyId = User.Identity.GetCompanyId().Value;
            return (await _ticketService.GetAllTicketsByCompanyAsync(companyId)).Any(t => t.Id == id);
        }

        [HttpPost]
        public async Task<JsonResult> GglTicketsByProject()
        {
            int companyId = User.Identity.GetCompanyId().Value;

            List<object> chartData = new();
            chartData.Add(new object[] { "Project", "Count" });

            try
            {
                int ticketCount;

                foreach (Project project in await _projectService.GetAllProjectsByCompanyAsync(companyId))
                {
                    ticketCount = project.Tickets.Count;
                    chartData.Add(new object[] { project.Name, ticketCount });
                    ticketCount = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Project");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }

        [HttpPost]
        public async Task<JsonResult> GglOpenTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            List<object> chartData = new();
            try
            {
                List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);
                int openTickets = (await _ticketService.GetAllTicketsByStatusAsync(companyId, nameof(BTTicketStatus.New))).Count;
                int dblCount = tickets.Count;
                
                var tckFraction = (double)openTickets / (double)dblCount;
                tckFraction = Math.Round(tckFraction, 2, MidpointRounding.AwayFromZero);
                tckFraction *= 100;
                var newString = tckFraction.ToString();
                newString = newString.Substring(0, 2) + "%";

                chartData.Add(new object[] { "", "OpenTicketCount" });
                chartData.Add(new object[] { newString, openTickets });
                chartData.Add(new object[] { String.Empty, (tickets.Count - openTickets) });
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Status");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }

        [HttpPost]
        public async Task<JsonResult> GglDevTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            List<object> chartData = new();
            try
            {
                List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);
                int devTickets = (await _ticketService.GetAllTicketsByStatusAsync(companyId, nameof(BTTicketStatus.Development))).Count;
                int dblCount = tickets.Count;

                var tckFraction = (double)devTickets / (double)dblCount;
                tckFraction = Math.Round(tckFraction, 2, MidpointRounding.AwayFromZero);
                tckFraction *= 100;
                var newString = tckFraction.ToString();
                newString = newString.Substring(0, 2) + "%";

                chartData.Add(new object[] { "", "DevelopmentTicketCount" });
                chartData.Add(new object[] { newString, devTickets });
                chartData.Add(new object[] { String.Empty, (tickets.Count - devTickets) });
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Status");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }

        [HttpPost]
        public async Task<JsonResult> GglResolvedTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            List<object> chartData = new();
            try
            {
                List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);
                int resolvedTickets = (await _ticketService.GetAllTicketsByStatusAsync(companyId, nameof(BTTicketStatus.Resolved))).Where(t => t.Archived == false).Count();
                int dblCount = tickets.Count;
                double tckFraction;
                if(resolvedTickets != 0)
                {
                    tckFraction = (double)resolvedTickets / (double)dblCount;
                    tckFraction = Math.Round(tckFraction, 2, MidpointRounding.AwayFromZero);
                    tckFraction *= 100;
                }
                else
                {
                    tckFraction = 0;
                }
                
                string newString = tckFraction.ToString();
                newString = (newString.Length < 2) ? newString + "%" : newString.Substring(0, 2) + "%";

                chartData.Add(new object[] { "", "ResolvedTicketCount" });
                chartData.Add(new object[] { newString, resolvedTickets });
                chartData.Add(new object[] { String.Empty, tickets.Count - resolvedTickets });
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Status");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }

        [HttpPost]
        public async Task<JsonResult> GglUserTicketsByProject()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            BTUser btUser = await _userManager.GetUserAsync(User);

            List<object> chartData = new();
            chartData.Add(new object[] { "Project", "Count" });

            try
            {
                int ticketCount = 0;

                foreach (Project project in await _projectService.GetAllProjectsByCompanyAsync(companyId))
                {
                    foreach(Ticket ticket in project.Tickets)
                    {
                        if(ticket.DeveloperUserId == btUser.Id)
                        {
                            ticketCount++;
                        }
                        else if (ticket.OwnerUserId == btUser.Id)
                        {
                            ticketCount++;
                        }
                        else if (await _projectService.IsAssignedProjectManagerAsync(btUser.Id,project.Id))
                        {
                            ticketCount++;
                        }
                        else
                        {
                        }
                    }
                    chartData.Add(new object[] { project.Name, ticketCount });
                    ticketCount = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Project");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }

        [HttpPost]
        public async Task<JsonResult> GglUserOpenTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            BTUser btUser = await _userManager.GetUserAsync(User);

            List<object> chartData = new();
            try
            {
                int openTickets = 0;

                List<Ticket> userTickets = await _ticketService.GetTicketsByUserIdAsync(btUser.Id, companyId);
                foreach(Ticket ticket in userTickets)
                {
                    if(ticket.TicketStatus.Name == nameof(BTTicketStatus.New))
                    {
                        openTickets++;
                    }
                }
                int dblCount = userTickets.Count;

                double tckFraction;
                if (openTickets != 0)
                {
                    tckFraction = (double)openTickets / (double)dblCount;
                    tckFraction = Math.Round(tckFraction, 2, MidpointRounding.AwayFromZero);
                    tckFraction *= 100;
                }
                else
                {
                    tckFraction = 0;
                }

                var newString = tckFraction.ToString();
                newString = (newString.Length < 2) ? newString + "%" : newString.Substring(0, 2) + "%";

                chartData.Add(new object[] { "", "OpenTicketCount" });
                chartData.Add(new object[] { newString, openTickets });
                chartData.Add(new object[] { String.Empty, (userTickets.Count - openTickets) });
            }
            catch (Exception ex)
            {
                Console.WriteLine("*************  ERROR  *************");
                Console.WriteLine("Error Counting Tickets By Status");
                Console.WriteLine(ex.Message);
                Console.WriteLine("***********************************");
                throw;
            }

            return Json(chartData);
        }
    }
    
}
