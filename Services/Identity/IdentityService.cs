using modulum.Application.Configurations;
using modulum.Application.Interfaces.Services.Identity;
using modulum.Application.Requests.Identity;
using modulum.Application.Responses.Identity;
using modulum.Infrastructure.Models.Identity;
using modulum.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using modulum.Application.Interfaces.Repositories;
using modulum.Shared.Models;
using System.Runtime.InteropServices;
using modulum.Shared.Constants.Application;

namespace modulum.Infrastructure.Services.Identity
{
    public class IdentityService : ITokenService
    {
        private const string InvalidErrorMessage = "E-mail ou senha inválidos.";

        private readonly UserManager<ModulumUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppConfiguration _appConfig;
        private readonly SignInManager<ModulumUser> _signInManager;

        public IdentityService(
            UserManager<ModulumUser> userManager, RoleManager<IdentityRole> roleManager,
            IOptions<AppConfiguration> appConfig, SignInManager<ModulumUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _appConfig = appConfig.Value;
            _signInManager = signInManager;
        }

        public async Task<Result<TokenResponse>> LoginAsync(TokenRequest model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                var fields = new Dictionary<string, string> { { "Email", "Usuário não encontrado." } };
                return await Result<TokenResponse>.FailAsync("Usuario não encontrado.", fields);
            }
            //if (!user.IsActive)
            //{
            //    return await Result<TokenResponse>.FailAsync(_localizer["User Not Active. Please contact the administrator."]);
            //}
            if (!user.EmailConfirmed)
            {
                var fields = new Dictionary<string, string> { { "Email", "E-Mail não confirmado." } };
                return await Result<TokenResponse>.FailAsync("E-Mail não confirmado.", fields);
            }
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                var fields = new Dictionary<string, string> { { "Password", "Senha inválida." } };
                return await Result<TokenResponse>.FailAsync("Senha inválida.", fields);
            }

            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            await _userManager.UpdateAsync(user);

            var token = await GenerateJwtAsync(user);
            var response = new TokenResponse { Token = token, RefreshToken = user.RefreshToken, RefreshTokenExpiryTime = user.RefreshTokenExpiryTime };
            return await Result<TokenResponse>.SuccessAsync(response);
        }

        public async Task<Result<TokenResponse>> GetRefreshTokenAsync(RefreshTokenRequest model)
        {
            if (model is null)
            {
                return await Result<TokenResponse>.FailAsync("Client Token inválido.");
            }
            var userPrincipal = GetPrincipalFromExpiredToken(model.Token);
            var userEmail = userPrincipal.FindFirstValue(ClaimTypes.Email);
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
            {
                var fields = new Dictionary<string, string> { { "Email", "Usuário não encontrado." } };
                return await Result<TokenResponse>.FailAsync("Usuário não encontrado.", fields);
            }
            if (user.RefreshToken != model.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.Now)
            {
                return await Result<TokenResponse>.FailAsync("Token inválido.");
            }
            var token = GenerateEncryptedToken(GetSigningCredentials(), await GetClaimsAsync(user));
            user.RefreshToken = GenerateRefreshToken();
            await _userManager.UpdateAsync(user);

            var response = new TokenResponse { Token = token, RefreshToken = user.RefreshToken, RefreshTokenExpiryTime = user.RefreshTokenExpiryTime };
            return await Result<TokenResponse>.SuccessAsync(response);
        }

        private async Task<string> GenerateJwtAsync(ModulumUser user)
        {
            var token = GenerateEncryptedToken(GetSigningCredentials(), await GetClaimsAsync(user));
            return token;
        }

        private async Task<IEnumerable<Claim>> GetClaimsAsync(ModulumUser user)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = new List<Claim>();
            var permissionClaims = new List<Claim>();
            foreach (var role in roles)
            {
                roleClaims.Add(new Claim(ClaimTypes.Role, role));
                var thisRole = await _roleManager.FindByNameAsync(role);
                //var allPermissionsForThisRoles = await _roleManager.GetClaimsAsync(thisRole);
                //permissionClaims.AddRange(allPermissionsForThisRoles);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.NomeCompleto)
            }
            .Union(userClaims)
            .Union(roleClaims);
            //.Union(permissionClaims);

            return claims;
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private string GenerateEncryptedToken(SigningCredentials signingCredentials, IEnumerable<Claim> claims)
        {
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(2),
                audience: "https://localhost:7051", // TODO ponto onde deve ser alterado para aceitar chamada do front do azure
                issuer: "https://localhost:7051",
                signingCredentials: signingCredentials
            );
            var tokenHandler = new JwtSecurityTokenHandler();
            var encryptedToken = tokenHandler.WriteToken(token);
            return encryptedToken;
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable(ApplicationConstants.Variable.ModulumSecretJWT) ?? _appConfig.Secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero,
                ValidateLifetime = false
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Token Inválido");
            }

            return principal;
        }

        private SigningCredentials GetSigningCredentials()
        {
            //string ramdumNumber = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            //var secret2 = Encoding.UTF8.GetBytes(ramdumNumber);
            var secret = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable(ApplicationConstants.Variable.ModulumSecretJWT) ?? _appConfig.Secret);
            return new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
        }


        // Método para realizar "logout"
        //public async Task<UserSignUpResponse> SignUp(UserSignUpRequest request, CancellationToken token)
        //{
        //    var isUserNameExist = await _unitOfWork.UserRepository.AnyAsync(x => x.UserName == request.UserName);
        //    if (isUserNameExist)
        //        throw UserException.UserAlreadyExistsException(request.UserName);
        //
        //    var isEmailExist = await _unitOfWork.UserRepository.AnyAsync(x => x.UserName == request.Email);
        //    if (isEmailExist)
        //        throw UserException.UserAlreadyExistsException(request.Email);
        //
        //    var user = _mapper.Map<User>(request);
        //    user.Password = user.Password.Hash();
        //    await _unitOfWork.ExecuteTransactionAsync(async () => await _unitOfWork.UserRepository.AddAsync(user), token);
        //
        //    var response = _mapper.Map<UserSignUpResponse>(user);
        //
        //    return response;
        //}
    }
}