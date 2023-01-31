using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.Design;

namespace BugTracker.Services
{
    public class BTInviteService : IBTInviteService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBTProjectService _projectService;

        public BTInviteService(ApplicationDbContext context,
                               IBTProjectService projectService)
        {
            _context = context;
            _projectService = projectService;
        }

        #region Accept Invite 
        public async Task<bool> AcceptInviteAsync(Guid? token, string userId, int companyId)
        {
            // userId means that this person already went through registration process. 
            Invite? invite = await _context.Invites.FirstOrDefaultAsync(i => i.CompanyToken == token);

            if(invite == null)
            {
                return false;
            }

            try
            {
                invite.IsValid = false;
                invite.JoinDate = DateTime.Today;
                invite.InviteeId = userId;

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting invite.");
                Console.WriteLine(ex.Message);
                throw;
            }
        }
        #endregion

        #region Add New Invite
        public async Task AddNewInviteAsync(Invite invite)
        {
            try
            {
                await _context.Invites.AddAsync(invite);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("&&&&&&&&&&&&&&&> ERROR ADDING NEW INVITE <&&&&&&&&&&&&&&&");
                Console.WriteLine(ex.Message);
                Console.WriteLine("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
                throw;
            }
        }
        #endregion

        #region Any Invite
        public async Task<bool> AnyInviteAsync(Guid token, string email, int companyId)
        {
            try
            {
                // anyasync allows us to specify what to search for within the group of invites. 
                bool result = await _context.Invites.Where(i => i.CompanyId == companyId)
                                                    .AnyAsync(i => i.CompanyToken == token && i.InviteeEmail == email);

                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Get All Invites By Company
        public async Task<List<Invite>> GetAllInvitesByCompanyAsync(int companyId)
        {

            try
            {
                List<Invite> invites = await _context.Invites.Where(i => i.CompanyId == companyId)
                                                             .Include(i => i.Company)
                                                             .Include(i => i.Project)
                                                             .Include(i => i.Invitor)
                                                             .Include(i => i.Invitee)
                                                             .ToListAsync();    
                return invites;
            }
            catch (Exception)
            {

                throw;
            }
            
        }
        #endregion

        #region Get Invite (inviteId)
        public async Task<Invite> GetInviteAsync(int inviteId, int companyId)
        {
            try
            {
                Invite invite = await _context.Invites.Where(i => i.CompanyId == companyId)
                                                      .Include(i => i.Company)
                                                      .Include(i => i.Project)
                                                      .Include(i => i.Invitor)
                                                      .Include(i => i.Invitee)
                                                      .FirstOrDefaultAsync(i => i.Id == inviteId);
                return invite;
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Get Invite (token, email)
        public async Task<Invite> GetInviteAsync(Guid token, string email, int companyId)
        {
            try
            {
                Invite invite = await _context.Invites.Where(i => i.CompanyId == companyId)
                                                      .Include(i => i.Company)
                                                      .Include(i => i.Project)
                                                      .Include(i => i.Invitor)
                                                      .FirstOrDefaultAsync(i => i.CompanyToken == token && i.InviteeEmail == email);
                return invite;
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Validate Invite Code
        public async Task<bool> ValidateInviteCodeAsync(Guid? token)
        {
            if(token == null)
            {
                return false;
            }

            bool result = false;

            Invite invite = await _context.Invites.FirstOrDefaultAsync(i => i.CompanyToken == token);

            if (invite != null)
            {
                // determine invite date
                DateTime inviteDate = invite.InviteDate.Date;

                // custom valudation of invite based on issued date
                // In this case, we are allowing an invite to be valid for 7 days
                bool validDate = (DateTime.UtcNow - inviteDate).TotalDays <= 7;
                // if invite is not expired, check to see if it has been used. If not, IsValid is true.
                if (validDate)
                {
                    result = invite.IsValid;
                }
                
            }

            return result;
        }
        #endregion
    }
}
