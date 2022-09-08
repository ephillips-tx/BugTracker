#nullable disable
using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Models.Enums;
using BugTracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Services
{
    public class BTProjectService : IBTProjectService
    {
        #region Properties
        private readonly ApplicationDbContext _context;
        private readonly IBTRolesService _rolesService;
        #endregion

        #region Constructor
        public BTProjectService(ApplicationDbContext context, 
                                IBTRolesService rolesService)
        {
            _context = context;
            _rolesService = rolesService;
        }
        #endregion

        #region Add New Project
        // CRUD - CREATE
        public async Task AddNewProjectAsync(Project project)
        {
            _context.Add(project);
            await _context.SaveChangesAsync();
        }
        #endregion

        public async Task<bool> AddProjectManagerAsync(string userId, int projectId)
        {
            // Remove current PM if needed
            BTUser currentPM = await GetProjectManagerAsync(projectId);

            if (currentPM != null)
            {
                try
                {
                    await RemoveProjectManagerAsync(projectId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error removing current PM. - Error: {ex.Message}");
                    return false;
                }
            }

            // Add new PM: Because of the way this method is called, the userId
            // passed to the method should already have the role of PM
            try
            {
                await AddUserToProjectAsync(userId, projectId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding new PM. - Error: {ex.Message}");
                return false;
            }

        }

        public async Task<bool> AddUserToProjectAsync(string userId, int projectId)
        {
            BTUser user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if(user != null)
            {
                Project project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (!await IsUserOnProjectAsync(userId, projectId))
                {
                    try
                    {
                        project.Members.Add(user);
                        await _context.SaveChangesAsync();
                        return true;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                else
                {
                    return false;
                }
            } 
            else
            {
                return false;
            }

        }

        // CRUD - DELETE
        public async Task ArchiveProjectAsync(Project project)
        {
            try
            {
                // When archived, older records are querried less frequently, increasing speed.
                project.Archived = true;
                await UpdateProjectAsync(project);

                // Archive the tickets for the project
                foreach(Ticket ticket in project.Tickets)
                {
                    ticket.ArchivedByProject = true;
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();
                }
                
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<BTUser>> GetAllProjectMembersExceptPMAsync(int projectId)
        {
            List<BTUser> developers = await GetProjectMembersByRoleAsync(projectId, Roles.Developer.ToString());
            List<BTUser> submitters = await GetProjectMembersByRoleAsync(projectId, Roles.Submitter.ToString());
            List<BTUser> admins = await GetProjectMembersByRoleAsync(projectId, Roles.Admin.ToString());

            // Concat method returns type IEnumberable &> convert to a list
            List<BTUser> teamMembers = developers.Concat(submitters).Concat(admins).ToList();

            return teamMembers;
        }

        public async Task<List<Project>> GetAllProjectsByCompanyAsync(int companyId)
        {
            // similar to BTCompanyInfoService because it may be used where BTCompanyInfoService is not used

            List<Project> projects = await _context.Projects.Where(p => p.CompanyId == companyId && p.Archived == false)  // Include = Eager Loading...
                                                            .Include(p => p.Members)               // Which members on this project
                                                            .Include(p => p.Tickets)               // Which tickets are linked to this project
                                                                .ThenInclude(t => t.Comments)         // Get comments associated with ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.Attachments)      // Get ticket attachments
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.Notifications)    // Get ticket notifications
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.History)          // Get ticket history
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.DeveloperUser)    // Get ticket developer
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.OwnerUser)        // Get ticket owner
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketStatus)     // Get status associated with ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketPriority)   // Get priority of ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketType)       // Get type of ticket
                                                            .Include(p => p.ProjectPriority)       // What is the priority of the project
                                                            .ToListAsync();                        // add results to list
            return projects;
        }

        public async Task<List<Project>> GetAllProjectsByPriority(int companyId, string priorityName)
        {
            List<Project> projects = await GetAllProjectsByCompanyAsync(companyId);
            int priorityId = await LookupProjectPriorityId(priorityName);

            return projects.Where(p => p.ProjectPriorityId == priorityId).ToList();
        }

        public async Task<List<Project>> GetArchivedProjectsByCompanyAsync(int companyId)
        {
            List<Project> projects = await _context.Projects.Where(p => p.CompanyId == companyId && p.Archived == true)  // Include = Eager Loading...
                                                            .Include(p => p.Members)               // Which members on this project
                                                            .Include(p => p.Tickets)               // Which tickets are linked to this project
                                                                .ThenInclude(t => t.Comments)         // Get comments associated with ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.Attachments)      // Get ticket attachments
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.Notifications)    // Get ticket notifications
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.History)          // Get ticket history
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.DeveloperUser)    // Get ticket developer
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.OwnerUser)        // Get ticket owner
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketStatus)     // Get status associated with ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketPriority)   // Get priority of ticket
                                                            .Include(p => p.Tickets)
                                                                .ThenInclude(t => t.TicketType)       // Get type of ticket
                                                            .Include(p => p.ProjectPriority)       // What is the priority of the project
                                                            .ToListAsync();                        // add results to list

            return projects;
        }

        public Task<List<BTUser>> GetDevelopersOnProjectAsync(int projectId)
        {
            throw new NotImplementedException();
        }

        #region Get Project By Id
        // CRUD - READ
        public async Task<Project> GetProjectByIdAsync(int projectId, int companyId)
        {
            //Project project = await _context.Projects
            //                                .Include(p => p.Tickets)
            //                                .Include(p => p.Members)
            //                                .Include(p => p.ProjectPriority)
            //                                .FirstOrDefaultAsync(p => p.Id == projectId && p.CompanyId == companyId);

            Project project = await _context.Projects
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.TicketPriority)
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.TicketStatus)
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.TicketType)
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.DeveloperUser)
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.OwnerUser)
                                            .Include(p => p.Members)
                                            .Include(p => p.ProjectPriority)
                                            .FirstOrDefaultAsync(p => p.Id == projectId && p.CompanyId == companyId);
                                
            return project;
        }
        #endregion

        public async Task<BTUser> GetProjectManagerAsync(int projectId)
        {
            // go to Projects table, find the project where id matches
            // bring the members associated with that project.
            Project project = await _context.Projects
                                            .Include(p => p.Members)
                                            .FirstOrDefaultAsync(p => p.Id == projectId);

            foreach(BTUser member in project?.Members)
            {
                if (await _rolesService.IsUserInRoleAsync(member, Roles.ProjectManager.ToString()))
                {
                    return member;
                }
            }
            return null;
        }

        public async Task<List<BTUser>> GetProjectMembersByRoleAsync(int projectId, string role)
        {
            // including members allows us to know which users are on the project
            Project project = await _context.Projects
                                            .Include(p => p.Members)
                                            .FirstOrDefaultAsync(p => p.Id == projectId);

            List<BTUser> members = new();

            foreach(var user in project.Members)
            {
                if(await _rolesService.IsUserInRoleAsync(user, role))
                {
                    members.Add(user);
                }
            }

            return members;
        }

        #region Get Unassigned Projects
        public async Task<List<Project>> GetUnassignedProjectsAsync(int companyId)
        {
            List<Project> result = new();
            List<Project> projects = new();

            try
            {
                projects = await _context.Projects
                                         .Include(p => p.ProjectPriority)
                                         .Where(p => p.CompanyId == companyId)
                                         .ToListAsync();
                foreach(Project project in projects)
                {
                    if((await GetProjectMembersByRoleAsync(project.Id, nameof(Roles.ProjectManager))).Count == 0)
                    {
                        result.Add(project);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }
        #endregion

        public Task<List<BTUser>> GetSubmittersOnProjectAsync(int projectId)
        {
            throw new NotImplementedException();
        }

        public async Task<List<Project>> GetUserProjectsAsync(string userId)
        {
            try
            {
                // a check of the projects table where the user ID matches could be used
                // but then you couldn't add in the extra information that we care about.
                // This code goes to the user table, grabs related info, checks the user
                // id for a match, gets all of the projects for that user & returns a list

                List<Project> userProjects = (await _context.Users
                    .Include(u => u.Projects)
                        .ThenInclude(p => p.Company)
                    .Include(u => u.Projects)
                        .ThenInclude(p => p.Members)
                    .Include(u => u.Projects)
                        .ThenInclude(p => p.Tickets)
                    .Include(u => u.Projects)
                        .ThenInclude(t => t.Tickets)
                            .ThenInclude(t => t.DeveloperUser)
                    .Include(u => u.Projects)
                        .ThenInclude(t => t.Tickets)
                            .ThenInclude(t => t.OwnerUser)
                    .Include(u => u.Projects)
                        .ThenInclude(t => t.Tickets)
                            .ThenInclude(t => t.TicketPriority)
                    .Include(u => u.Projects)
                        .ThenInclude(t => t.Tickets)
                            .ThenInclude(t => t.TicketStatus)
                    .Include(u => u.Projects)
                        .ThenInclude(t => t.Tickets)
                            .ThenInclude(t => t.TicketType)
                    .FirstOrDefaultAsync(u => u.Id == userId)).Projects.ToList();

                return userProjects;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"**** ERROR **** - Error getting user projects list. --> {ex.Message}");
                throw;
            }
        }

        public async Task<List<BTUser>> GetUsersNotOnProjectAsync(int projectId, int companyId)
        {
            // NOT on project
            List<BTUser> users = await _context.Users.Where(u => u.Projects.All(p => p.Id != projectId)).ToListAsync();

            return users.Where(u => u.CompanyId == companyId).ToList();
        }

        public async Task<bool> IsAssignedProjectManagerAsync(string userId, int projectId)
        {
            try
            {
                string projectManagerId = (await GetProjectManagerAsync(projectId))?.Id;

                if (projectManagerId == userId)
                {
                    return true;
                }
                return false;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<bool> IsUserOnProjectAsync(string userId, int projectId)
        {
            Project project = await _context.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == projectId);

            bool result = false;

            if(project != null)
            {
                result = project.Members.Any(m => m.Id == userId);
            }

            return result;
        }

        public async Task<int> LookupProjectPriorityId(string priorityName)
        {
            int priorityId = (await _context.ProjectPriorities.FirstOrDefaultAsync(p => p.Name == priorityName)).Id;
            return priorityId;
        }

        public async Task RemoveProjectManagerAsync(int projectId)
        {
            // go to Projects table (looking for users &> get members of the project)
            // match based on projectId parameter
            Project project = await _context.Projects
                                            .Include(p => p.Members)
                                            .FirstOrDefaultAsync(p => p.Id == projectId);

            try
            {
                foreach(BTUser member in project?.Members)
                {
                    if(await _rolesService.IsUserInRoleAsync(member, Roles.ProjectManager.ToString()))
                    {
                        await RemoveUserFromProjectAsync(member.Id, projectId);
                    }
                }
            }
            catch 
            {
                throw;
            }
        }

        public async Task RemoveUserFromProjectAsync(string userId, int projectId)
        {
            try
            {
                BTUser user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                Project project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                try
                {
                    if (await IsUserOnProjectAsync(userId, projectId))
                    {
                        project.Members.Remove(user);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception)
                {
                    throw;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"**** ERROR **** - Error removing user from project. ---> {ex.Message}");
            }
        }

        public async Task RemoveUsersFromProjectByRoleAsync(string role, int projectId)
        {
            try
            {
                List<BTUser> members = await GetProjectMembersByRoleAsync(projectId, role);
                Project project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                                    
                foreach (BTUser btUser in members)
                {
                    try
                    {
                        project.Members.Remove(btUser);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"**** ERROR **** - Error removing users from project. --> {ex.Message}");
                throw;
            }
        }

        public async Task RestoreProjectAsync(Project project)
        {
            try
            {
                project.Archived = false;
                await UpdateProjectAsync(project);

                // Archive the tickets for the project
                foreach (Ticket ticket in project.Tickets)
                {
                    ticket.ArchivedByProject = false;
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        // CRUD - EDIT
        public async Task UpdateProjectAsync(Project project)
        {
            _context.Update(project);
            await _context.SaveChangesAsync();
        }
    }
}
