﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using modulum.Application.Interfaces.Services;
using modulum.Application.Interfaces.Services.Identity;
using modulum.Application.Requests.Identity;
using modulum.Application.Responses.Identity;
using modulum.Infrastructure.Contexts;
using modulum.Infrastructure.Models.Identity;
using modulum.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace modulum.Infrastructure.Services.Identity
{
    //public class RoleClaimService : IRoleClaimService
    //{
    //    private readonly IMapper _mapper;
    //    private readonly ICurrentUserService _currentUserService;
    //    private readonly ModulumContext _db;
    
    //    public RoleClaimService(
    //        IMapper mapper,
    //        ICurrentUserService currentUserService,
    //        ModulumContext db)
    //    {
    //        _mapper = mapper;
    //        _currentUserService = currentUserService;
    //        _db = db;
    //    }
    
    //    public async Task<Result<List<RoleClaimResponse>>> GetAllAsync()
    //    {
    //        var roleClaims = await _db.RoleClaims.ToListAsync();
    //        var roleClaimsResponse = _mapper.Map<List<RoleClaimResponse>>(roleClaims);
    //        return await Result<List<RoleClaimResponse>>.SuccessAsync(roleClaimsResponse);
    //    }
    
    //    public async Task<int> GetCountAsync()
    //    {
    //        var count = await _db.RoleClaims.CountAsync();
    //        return count;
    //    }
    
    //    public async Task<Result<RoleClaimResponse>> GetByIdAsync(int id)
    //    {
    //        var roleClaim = await _db.RoleClaims
    //            .SingleOrDefaultAsync(x => x.Id == id);
    //        var roleClaimResponse = _mapper.Map<RoleClaimResponse>(roleClaim);
    //        return await Result<RoleClaimResponse>.SuccessAsync(roleClaimResponse);
    //    }
    
    //    public async Task<Result<List<RoleClaimResponse>>> GetAllByRoleIdAsync(string roleId)
    //    {
    //        var roleClaims = await _db.RoleClaims
    //            .Include(x => x.Role)
    //            .Where(x => x.RoleId == roleId)
    //            .ToListAsync();
    //        var roleClaimsResponse = _mapper.Map<List<RoleClaimResponse>>(roleClaims);
    //        return await Result<List<RoleClaimResponse>>.SuccessAsync(roleClaimsResponse);
    //    }
    
    //    public async Task<Result<string>> SaveAsync(RoleClaimRequest request)
    //    {
    //        if (string.IsNullOrWhiteSpace(request.RoleId))
    //        {
    //            return await Result<string>.FailAsync("Role is required.");
    //        }
    
    //        if (request.Id == 0)
    //        {
    //            var existingRoleClaim =
    //                await _db.RoleClaims
    //                    .SingleOrDefaultAsync(x =>
    //                        x.RoleId == request.RoleId && x.ClaimType == request.Type && x.ClaimValue == request.Value);
    //            if (existingRoleClaim != null)
    //            {
    //                return await Result<string>.FailAsync("Similar Role Claim already exists.");
    //            }
    //            var roleClaim = _mapper.Map<ModulumRoleClaim>(request);
    //            await _db.RoleClaims.AddAsync(roleClaim);
    //            await _db.SaveChangesAsync(_currentUserService.UserId);
    //            return await Result<string>.SuccessAsync(string.Format("Role Claim {0} created.", request.Value));
    //        }
    //        else
    //        {
    //            var existingRoleClaim =
    //                await _db.RoleClaims
    //                    .Include(x => x.Role)
    //                    .SingleOrDefaultAsync(x => x.Id == request.Id);
    //            if (existingRoleClaim == null)
    //            {
    //                return await Result<string>.SuccessAsync("Role Claim does not exist.");
    //            }
    //            else
    //            {
    //                existingRoleClaim.ClaimType = request.Type;
    //                existingRoleClaim.ClaimValue = request.Value;
    //                existingRoleClaim.Group = request.Group;
    //                existingRoleClaim.Description = request.Description;
    //                existingRoleClaim.RoleId = request.RoleId;
    //                _db.RoleClaims.Update(existingRoleClaim);
    //                await _db.SaveChangesAsync(_currentUserService.UserId);
    //                return await Result<string>.SuccessAsync(string.Format("Role Claim {0} for Role {1} updated.", request.Value, existingRoleClaim.Role.Name));
    //            }
    //        }
    //    }
    
    //    public async Task<Result<string>> DeleteAsync(int id)
    //    {
    //        var existingRoleClaim = await _db.RoleClaims
    //            .Include(x => x.Role)
    //            .FirstOrDefaultAsync(x => x.Id == id);
    //        if (existingRoleClaim != null)
    //        {
    //            _db.RoleClaims.Remove(existingRoleClaim);
    //            await _db.SaveChangesAsync(_currentUserService.UserId);
    //            return await Result<string>.SuccessAsync(string.Format("Role Claim {0} for {1} Role deleted.", existingRoleClaim.ClaimValue, existingRoleClaim.Role.Name));
    //        }
    //        else
    //        {
    //            return await Result<string>.FailAsync("Role Claim does not exist.");
    //        }
    //    }
    //}
}