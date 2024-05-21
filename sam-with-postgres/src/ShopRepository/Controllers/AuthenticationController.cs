using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopRepository.Data;
using ShopRepository.Dtos;
using ShopRepository.Services;

namespace ShopRepository.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthenticationController(IShopRepo repo, IConfiguration config, CognitoService cognito)
    : ControllerBase
{
    [HttpPost("CustomerLogin")]
    public async Task<IActionResult> Login([FromBody] CustomerInput customer)
    {
        try
        {
            var session = await cognito.Login(customer.Email, customer.Pass);
            return Ok(new
            {
                customer.Email,
                Token = session.IdToken,
                session.AccessToken,
                session.RefreshToken
            });
        }
        catch (Exception e)
        {
            return Unauthorized(e.Message);
        }
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpGet("ValidateCustomer")]
    public async Task<IActionResult> CheckLogin()
    {
        var userSubClaim = User.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value;
        var accessToken = (User.Identity as ClaimsIdentity)?.BootstrapContext?.ToString();

        var cognitoUser = await cognito.GetUser(userSubClaim, accessToken);
        if (cognitoUser == null) return Unauthorized("Cannot find user in cognito pool");
        if (!cognitoUser.Attributes.TryGetValue("email", out var email)) return Unauthorized("Malformed user");
        
        var customer = await repo.GetCustomerFromEmail(email);
        if (customer == null) return Unauthorized("User not found in database");

        var returnCustomer = new CustomerSession
        {
            Email = customer.Email,
            Fname = customer.FirstName,
            Lname = customer.LastName,
            Id = customer.Id.ToString()
        };
        return Ok(returnCustomer);
    }

    [HttpPost("RegisterCustomer")]
    public async Task<IActionResult> Register([FromBody] CustomerInput customer)
    {
        Console.WriteLine("We out here");
        if (await repo.GetCustomerFromEmail(customer.Email) != null)
            return BadRequest("Customer with that email already exists.");
        try
        {
            var localSuccess = await repo.AddCustomer(customer);
            if (!localSuccess) BadRequest("Failed to add user to local database");

            var response = await cognito.Register(customer.Email, customer.Pass, customer.Fname, customer.Lname);
            if (response.HttpStatusCode != HttpStatusCode.OK)
                throw new Exception($"Failed to register user in cognito: {response}");

            return Ok(new { confirmed = response.UserConfirmed });
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            var cInDb = await repo.GetCustomerFromEmail(customer.Email);
            if (cInDb != null)
                await repo.DeleteCustomer(cInDb.Id);

            return BadRequest($"Sign up failed, please try again. {e.Message}");
        }
    }

    [HttpPost("ConfirmCustomer")]
    public async Task<IActionResult> Confirm([FromBody] Dictionary<string, string> confirm)
    {
        var email = confirm["Email"];
        var code = confirm["Code"];
        try
        {
            var response = await cognito.ConfirmUser(email, code);
            if (response.HttpStatusCode != HttpStatusCode.OK)
                throw new Exception($"Failed to confirm user in cognito: {response}");

            return Ok(response);
        }
        catch (Exception e)
        {
            return BadRequest($"Confirmation failed, please try again. {e.Message}");
        }
    }
}