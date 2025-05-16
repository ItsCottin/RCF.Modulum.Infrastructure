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
using modulum.Domain.Entities.DynamicEntity;
using modulum.Domain.Entities.Account;
using modulum.Infrastructure.Contexts;
using modulum.Shared.Models;
using nodulum.Application.Requests.Identity;
using static modulum.Shared.Constants.Permission.Permissions;
using modulum.Application.Requests.Account;
using modulum.Application.Interfaces.Repositories;
using RCF.Modulum.Shared.Constants.Email;

namespace modulum.Infrastructure.Services.Identity
{
    public class UserService : IUserService
    {
        //private readonly RoleManager<ModulumRole> _roleManager;
        private readonly UserManager<ModulumUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IMapper _mapper;
        private readonly ModulumContext _dbContext;
        private readonly ITwoFactorRepository _twoFactorRepository;

        public UserService(
            UserManager<ModulumUser> userManager,
            IMapper mapper,
            //RoleManager<ModulumRole> roleManager,
            IEmailService emailService,
            ModulumContext dbContext,
            ITwoFactorRepository twoFactorRepository)
        {
            _userManager = userManager;
            _mapper = mapper;
            //_roleManager = roleManager;
            _emailService = emailService;
            _dbContext = dbContext;
            _twoFactorRepository = twoFactorRepository;
        }

        public async Task<Result<List<UserResponse>>> GetAllAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = _mapper.Map<List<UserResponse>>(users);
            return await Result<List<UserResponse>>.SuccessAsync(result);
        }

        public async Task<IResult> FimRegisterAsync(FinishRegisterRequest request, string origin)
        {
            var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
            if (userWithSameEmail != null)
            {
                userWithSameEmail.IsCadastroFinalizado = true;
                var result = await _userManager.AddPasswordAsync(userWithSameEmail, request.Password);
                if (result.Succeeded)
                {
                    return await Result<string>.SuccessAsync(userWithSameEmail.Id.ToString(), string.Format("Cadastro do E-mail '{0}' finalizado.", userWithSameEmail.Email));
                }
                else
                {
                    return await Result.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
                }
            }
            else
            {
                return await Result.FailAsync(string.Format("E-mail '{0}' não cadastrado.", userWithSameEmail.Email));
            }
        }

        public bool ValidaCPF(string cpf)
        {
            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                return false;

            if (cpf.Distinct().Count() == 1)
                return false;

            for (int j = 9; j < 11; j++)
            {
                int soma = 0;
                for (int i = 0; i < j; i++)
                    soma += (cpf[i] - '0') * (j + 1 - i);

                int digito = soma % 11;
                digito = digito < 2 ? 0 : 11 - digito;

                if ((cpf[j] - '0') != digito)
                    return false;
            }
            return true;
        }

        public string AplicaMascaraCPF(string cpf)
        {
            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                return cpf;

            return $"{cpf.Substring(0, 3)}.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-{cpf.Substring(9, 2)}";
        }

        public async Task<IResult> CadastroExterno(CadastroExternoRequest request)
        {
            if (!ValidaCPF(request.Cpf))
                await Result.FailAsync(string.Format("O CPF '{0}' informado é inválido", AplicaMascaraCPF(request.Cpf)));

            var userWithSameCpf = await _userManager.Users.Where(u => u.Cpf == request.Cpf).FirstOrDefaultAsync();
            if (userWithSameCpf != null)
            {
                var fields = new Dictionary<string, string> { { "Cpf", string.Format("O CPF '{0}' já está registrado.", AplicaMascaraCPF(request.Cpf)) } };
                return await Result.FailAsync(string.Format("O CPF '{0}' já está registrado.", AplicaMascaraCPF(request.Cpf)), fields);
            }

            var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
            var user = new ModulumUser
            {
                Email = request.Email,
                EmailConfirmed = true,
                IsCadastroFinalizado = true,
                UserName = request.Email,
                NomeCompleto = request.Name,
                Cpf = AplicaMascaraCPF(request.Cpf),
                NormalizedUserName = request.Email.ToUpperInvariant()
            };
            if (userWithSameEmail == null)
            {
                var result = await _userManager.CreateAsync(user, request.Password);
                if (result.Succeeded)
                {
                    var UserRegistrado = await _userManager.FindByEmailAsync(user.Email);
                    if (UserRegistrado == null)
                    {
                        await Result.FailAsync(string.Format("Ocorreu um erro ao cadastrar o E-mail '{0}'", request.Email));
                    }
                    await _userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                    await _dbContext.SaveChangesAsync();
                    return await Result<string>.SuccessAsync(user.Id.ToString(), string.Format("E-mail '{0}' registrado com sucesso", user.Email));
                }
                else
                {
                    return await Result.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
                }
            }
            else
            {
                var fields = new Dictionary<string, string> { { "Email", string.Format("O E-mail '{0}' já está registrado.", request.Email) } };
                return await Result.FailAsync(string.Format("O E-mail '{0}' já está registrado.", request.Email), fields);
            }
        }

        public async Task<IResult> PreRegisterAsync(PreRegisterRequest request, string origin)
        {
            var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
            var user = new ModulumUser
            {
                Email = request.Email,
                EmailConfirmed = false,
                IsCadastroFinalizado = false,
                UserName = request.Email,
                NormalizedUserName = request.Email.ToUpperInvariant()
            };
            if (userWithSameEmail == null)
            {
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    var UserRegistrado = await _userManager.FindByEmailAsync(user.Email);
                    if (UserRegistrado == null)
                    {
                        await Result.FailAsync(string.Format("Ocorreu um erro ao cadastrar o E-mail '{0}'", request.Email));
                    }
                    var codeTwoFactor = new Random().Next(100000, 1000000).ToString();
                    TwoFactor twoFactor = new TwoFactor()
                    {
                        IdUser = UserRegistrado.Id,
                        IsUsed = false,
                        Code = codeTwoFactor
                    };
                    await _userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                    await _dbContext.TwoFactors.AddAsync(twoFactor);
                    await _dbContext.SaveChangesAsync();
                    string url = $"{Environment.GetEnvironmentVariable(ApplicationConstants.Variable.UrlClient)}/{user.Id}"; //newUser
                    var requestDto = new MailRequest
                    {
                        From = "modulumprojeto@gmail.com",
                        To = user.Email, //newUser
                        Subject = "Confirme seu E-mail",
                        Body = await _emailService.SubstituirCodigoNoHtml(codeTwoFactor)
                    };
                    var retunText = await _emailService.SendEmail(requestDto);
                    var fields = new Dictionary<string, string> { { "CodeTwoFactor", codeTwoFactor } };
                    return await Result<string>.SuccessAsync(user.Id.ToString(), fields, string.Format("E-mail '{0}' registrado. Por favor, verifique sua caixa de entrada para ativar seu cadastro", user.Email));
                }
                else
                {
                    return await Result.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
                }
            }
            else
            {
                var fields = new Dictionary<string, string> { { "Email", string.Format("O E-mail '{0}' já está registrado.", request.Email) } };
                return await Result.FailAsync(string.Format("O E-mail '{0}' já está registrado.", request.Email), fields);
            }
        }

        public async Task<IResult<UserResponse>> GetAsync(int userId)
        {
            var user = await _userManager.Users.Where(u => u.Id == userId).FirstOrDefaultAsync();
            var result = _mapper.Map<UserResponse>(user);
            return await Result<UserResponse>.SuccessAsync(result);
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

        public async Task<IResult> ConfirmEmailAsync(TwoFactorRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return await Result.FailAsync("Usuário não encontrado");

            TwoFactor twoFactor = await _twoFactorRepository.GetTwoFactorByUserId(user.Id);
            if (twoFactor == null)
                return await Result.FailAsync("Houve um erro, mais não é sua culpa");

            if (twoFactor.IsUsed)
            {
                var fields = new Dictionary<string, string> { { "Code", string.Format("O código '{0}' já foi utilizado", request.Code) } };
                return await Result.FailAsync(string.Format("O código '{0}' já foi utilizado", request.Code), fields);
            }

            if (twoFactor.Code != request.Code)
            {
                var fields = new Dictionary<string, string> { { "Code", string.Format("Código '{0}' incorreto", request.Code) } };
                return await Result.FailAsync(string.Format("Código '{0}' incorreto", request.Code), fields);
            }

            user.EmailConfirmed = true;
            twoFactor.IsUsed = true;
            var resultado = await _userManager.UpdateAsync(user);
            if (!resultado.Succeeded)
                return await Result.FailAsync(resultado.Errors.Select(a => a.Description.ToString()).ToList());

            await _twoFactorRepository.UpdateTwoFactor(twoFactor);
            return await Result.SuccessAsync(string.Format("E-mail '{0}' confirmado com sucesso", request.Email));
        }

        public async Task<IResult> IsEmailConfirmed(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                var fields = new Dictionary<string, string> { { "Email", "Não existe usuario com E - mail informado" } };
                return await Result.FailAsync("Não existe usuario com E-mail informado", fields);
            }
            return user.EmailConfirmed ? await Result.SuccessAsync("E-mail informado está confirmado") : await Result.FailAsync("E-mail informado não está confirmado, Por favor confirme seu e - mail na caixa de entrada.", new Dictionary<string, string> { { "Email", "E-mail informado não está confirmado, Por favor confirme seu e - mail na caixa de entrada." } });
        }

        public async Task<IResult> ChangePasswordAsync(ChangePasswordRequest model, string userId)
        {
            var user = await this._userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return await Result.FailAsync("Usuário não encontrado.");
            }

            var identityResult = await this._userManager.ChangePasswordAsync(
                user,
                model.Password,
                model.NewPassword);
            var errors = identityResult.Errors.Select(e => e.Description.ToString()).ToList();
            return identityResult.Succeeded ? await Result.SuccessAsync() : await Result.FailAsync(errors);
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

        //public async Task<IResult<UserRolesResponse>> GetRolesAsync(string userId)
        //{
        //    var viewModel = new List<UserRoleModel>();
        //    var user = await _userManager.FindByIdAsync(userId);
        //    var roles = await _roleManager.Roles.ToListAsync();
        //
        //    foreach (var role in roles)
        //    {
        //        var userRolesViewModel = new UserRoleModel
        //        {
        //            RoleName = role.Name
        //        };
        //        if (await _userManager.IsInRoleAsync(user, role.Name))
        //        {
        //            userRolesViewModel.Selected = true;
        //        }
        //        else
        //        {
        //            userRolesViewModel.Selected = false;
        //        }
        //        viewModel.Add(userRolesViewModel);
        //    }
        //    var result = new UserRolesResponse { UserRoles = viewModel };
        //    return await Result<UserRolesResponse>.SuccessAsync(result);
        //}

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
    }
}
