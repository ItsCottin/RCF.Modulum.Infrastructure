using modulum.Application.Interfaces.Services;
using modulum.Application.Interfaces.Services.Account;
using modulum.Infrastructure.Models.Identity;
using modulum.Application.Requests.Identity;
using modulum.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace modulum.Infrastructure.Services.Identity
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<ModulumUser> _userManager;
        private readonly SignInManager<ModulumUser> _signInManager;
        private readonly IStringLocalizer<AccountService> _localizer;

        public AccountService(
            UserManager<ModulumUser> userManager,
            SignInManager<ModulumUser> signInManager,
            IStringLocalizer<AccountService> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _localizer = localizer;
        }

        public async Task<IResult> ChangePasswordAsync(ChangePasswordRequest model, string userId)
        {
            var user = await this._userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return await Result.FailAsync(_localizer["User Not Found."]);
            }

            var identityResult = await this._userManager.ChangePasswordAsync(
                user,
                model.Password,
                model.NewPassword);
            var errors = identityResult.Errors.Select(e => _localizer[e.Description].ToString()).ToList();
            return identityResult.Succeeded ? await Result.SuccessAsync() : await Result.FailAsync(errors);
        }

        //public async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, string userId)
        //{
        //    if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        //    {
        //        var userWithSamePhoneNumber = await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);
        //        if (userWithSamePhoneNumber != null)
        //        {
        //            return await Result.FailAsync(string.Format(_localizer["Phone number {0} is already used."], request.PhoneNumber));
        //        }
        //    }
        //
        //    var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
        //    if (userWithSameEmail == null || userWithSameEmail.Id == userId)
        //    {
        //        var user = await _userManager.FindByIdAsync(userId);
        //        if (user == null)
        //        {
        //            return await Result.FailAsync(_localizer["User Not Found."]);
        //        }
        //        user.FirstName = request.FirstName;
        //        user.LastName = request.LastName;
        //        user.PhoneNumber = request.PhoneNumber;
        //        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        //        if (request.PhoneNumber != phoneNumber)
        //        {
        //            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
        //        }
        //        var identityResult = await _userManager.UpdateAsync(user);
        //        var errors = identityResult.Errors.Select(e => _localizer[e.Description].ToString()).ToList();
        //        await _signInManager.RefreshSignInAsync(user);
        //        return identityResult.Succeeded ? await Result.SuccessAsync() : await Result.FailAsync(errors);
        //    }
        //    else
        //    {
        //        return await Result.FailAsync(string.Format(_localizer["Email {0} is already used."], request.Email));
        //    }
        //}
    }
}