using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AutoMapper;
using modulum.Application.Exceptions;
using modulum.Application.Extensions;
using modulum.Application.Interfaces.Services;
using modulum.Application.Interfaces.Services.Identity;
using modulum.Application.Requests.Identity;
using modulum.Application.Requests.Mail;
using modulum.Application.Responses.Identity;
using modulum.Infrastructure.Models.Identity;
using modulum.Shared.Constants.Role;
using modulum.Shared.Wrapper;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using modulum.Application.Interfaces.Services.Account;
using modulum.Shared.Constants.Application;
using System.Net;
using modulum.Domain.Entities.MapCoreEntity;

namespace modulum.Infrastructure.Services.Identity
{
    public class UserService : IUserService
    {
        private readonly UserManager<ModulumUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UserService(
            UserManager<ModulumUser> userManager,
            IMapper mapper,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _mapper = mapper;
            _roleManager = roleManager;
            _emailService = emailService;
            _currentUserService = currentUserService;
        }

        public async Task<Result<List<UserResponse>>> GetAllAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = _mapper.Map<List<UserResponse>>(users);
            return await Result<List<UserResponse>>.SuccessAsync(result);
        }

        public async Task<IResult> RegisterAsync(RegisterRequest request, string origin)
        {
            var userWithSameUserName = await _userManager.FindByNameAsync(request.UserName);
            if (userWithSameUserName != null)
            {
                var fields = new Dictionary<string, object>{{ "UserName", string.Format("O nome de usuário '{0}' já existe.", request.UserName)} };
                return await Result.FailAsync(string.Format("O nome de usuário '{0}' já existe.", request.UserName), fields);
            }
            var user = new ModulumUser
            {
                Email = request.Email,
                NomeCompleto = request.NomeCompleto,
                UserName = request.UserName,
                EmailConfirmed = false                
            };

            var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
            if (userWithSameEmail == null)
            {
                var result = await _userManager.CreateAsync(user, request.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                    var confirmEmailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user); //newUser
                    var validEmailToken = WebUtility.UrlEncode(confirmEmailToken); // Solução dada pelo ChatGPT
                    //var encodeEmailToken = Encoding.UTF8.GetBytes(confirmEmailToken); // Problema com geração do token grande demais
                    //var validEmailToken = WebEncoders.Base64UrlEncode(encodeEmailToken);
                    string url = $"{Environment.GetEnvironmentVariable(ApplicationConstants.Variable.UrlClient)}/confirm-email?userId={user.Id}&token={validEmailToken}"; //newUser
                    
                    if (!request.EmailConfirmed)
                    {
                        var requestDto = new MailRequest
                        {
                            From = "modulumprojeto@gmail.com",
                            To = user.Email, //newUser
                            Subject = "Confirme seu E-mail",
                            Body = $@"Olá, <br /> <br />
Recebemos sua solicitação de registro para o nosso sistema Modulum. <br /> <br />
Para confirmar sua inscrição clique no link a seguir: <a href=""{url}"">confirme seu cadastro</a> <br /> <br />
Se você não solicitou esse registro, pode ignorar este e-mail com segurança. Outra pessoa pode ter digitado seu endereço de e-mail por engano."
                        };
                        var retunText = await _emailService.SendEmail(requestDto);
                        var fields = new Dictionary<string, object> { { "AtivacaoEmail", url } };
                        return await Result<string>.SuccessAsync(user.Id, string.Format("Usuário {0} registrado. Por favor, verifique sua caixa de entrada para ativar seu cadastro", user.UserName, fields));
                    }
                    return await Result<string>.SuccessAsync(user.Id, string.Format("Usuário {0} registrado.", user.UserName));
                }
                else
                {
                    return await Result.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
                }
            }
            else
            {
                var fields = new Dictionary<string, object> { { "Email", string.Format("O E-mail {0} já está registrado.", request.Email) } };
                return await Result.FailAsync(string.Format("O E-mail {0} já está registrado.", request.Email), fields);
            }
        }

        public async Task<IResult<UserResponse>> GetAsync(string userId)
        {
            var user = await _userManager.Users.Where(u => u.Id == userId).FirstOrDefaultAsync();
            var result = _mapper.Map<UserResponse>(user);
            return await Result<UserResponse>.SuccessAsync(result);
        }

        //public async Task<IResult> ToggleUserStatusAsync(ToggleUserStatusRequest request)
        //{
        //    var user = await _userManager.Users.Where(u => u.Id == request.UserId).FirstOrDefaultAsync();
        //    var isAdmin = await _userManager.IsInRoleAsync(user, RoleConstants.AdministratorRole);
        //    if (isAdmin)
        //    {
        //        return await Result.FailAsync("Administrators Profile's Status cannot be toggled");
        //    }
        //    if (user != null)
        //    {
        //        user.IsActive = request.ActivateUser;
        //        var identityResult = await _userManager.UpdateAsync(user);
        //    }
        //    return await Result.SuccessAsync();
        //}

        public async Task<IResult<UserRolesResponse>> GetRolesAsync(string userId)
        {
            var viewModel = new List<UserRoleModel>();
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _roleManager.Roles.ToListAsync();

            foreach (var role in roles)
            {
                var userRolesViewModel = new UserRoleModel
                {
                    RoleName = role.Name
                };
                if (await _userManager.IsInRoleAsync(user, role.Name))
                {
                    userRolesViewModel.Selected = true;
                }
                else
                {
                    userRolesViewModel.Selected = false;
                }
                viewModel.Add(userRolesViewModel);
            }
            var result = new UserRolesResponse { UserRoles = viewModel };
            return await Result<UserRolesResponse>.SuccessAsync(result);
        }

        //public async Task<IResult> UpdateRolesAsync(UpdateUserRolesRequest request)
        //{
        //    var user = await _userManager.FindByIdAsync(request.UserId);
        //    if (user.Email == "mukesh@blazorhero.com")
        //    {
        //        return await Result.FailAsync(_localizer["Not Allowed."]);
        //    }
        //
        //    var roles = await _userManager.GetRolesAsync(user);
        //    var selectedRoles = request.UserRoles.Where(x => x.Selected).ToList();
        //
        //    var currentUser = await _userManager.FindByIdAsync(_currentUserService.UserId);
        //    if (!await _userManager.IsInRoleAsync(currentUser, RoleConstants.AdministratorRole))
        //    {
        //        var tryToAddAdministratorRole = selectedRoles
        //            .Any(x => x.RoleName == RoleConstants.AdministratorRole);
        //        var userHasAdministratorRole = roles.Any(x => x == RoleConstants.AdministratorRole);
        //        if (tryToAddAdministratorRole && !userHasAdministratorRole || !tryToAddAdministratorRole && userHasAdministratorRole)
        //        {
        //            return await Result.FailAsync(_localizer["Not Allowed to add or delete Administrator Role if you have not this role."]);
        //        }
        //    }
        //
        //    var result = await _userManager.RemoveFromRolesAsync(user, roles);
        //    result = await _userManager.AddToRolesAsync(user, selectedRoles.Select(y => y.RoleName));
        //    return await Result.SuccessAsync(_localizer["Roles Updated"]);
        //}

        public async Task<IResult<string>> ConfirmEmailAsync(string userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId);
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            if (result.Succeeded)
            {
                return await Result<string>.SuccessAsync(user.Id, string.Format("Conta {0} confirmada com sucesso.", user.Email));
            }
            else
            {
                throw new ApiException(string.Format("Ocorreu um erro ao confirmar {0}", user.Email));
            }
        }

        public async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, string origin)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return await Result.FailAsync("Ocorreu um erro");
            }
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var route = "account/reset-password";
            var endpointUri = new Uri(string.Concat($"{origin}/", route));
            var passwordResetURL = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);
            var mailRequest = new MailRequest
            {
                Body = string.Format("Por favor, redefina sua senha <a href='{0}'>clicando aqui</a>.", HtmlEncoder.Default.Encode(passwordResetURL)),
                Subject = "Redefinir senha",
                To = request.Email
            };
            var retunText = await _emailService.SendEmail(mailRequest);
            return await Result.SuccessAsync("O e-mail de redefinição de senha foi enviado para seu e-mail autorizado.");
        }

        public async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return await Result.FailAsync("Ocorreu um erro!");
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
            if (result.Succeeded)
            {
                return await Result.SuccessAsync("Redefinição de senha bem-sucedida!");
            }
            else
            {
                return await Result.FailAsync("Ocorreu um erro!");
            }
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _userManager.Users.CountAsync();
            return count;
        }
    }
}
