using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public void SendVerificationEmail(string toEmail, string verifyUrl)
    {
        var fromEmail = _config["Email:Sender"];
        var appPassword = _config["Email:AppPassword"];

        var message = new MailMessage(fromEmail, toEmail)
        {
            Subject = "CDM Voting Account Verification",
            Body = $@"
            <p>Welcome to CDM Voting!</p>
            <p>Click the button below to verify your account:</p>
            <a href='{verifyUrl}' 
               style='background-color:#4CAF50;color:white;padding:10px 20px;
                      text-decoration:none;border-radius:5px;'>
                Verify Account
            </a>",
            IsBodyHtml = true
        };

        var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new NetworkCredential(fromEmail, appPassword),
            EnableSsl = true,
            Timeout = 5000
        };

        smtp.Send(message);
    }
    public void SendAuthenticationCode(string toEmail, string code)
    {
        var fromEmail = _config["Email:Sender"];
        var appPassword = _config["Email:AppPassword"];

        var message = new MailMessage(fromEmail, toEmail)
        {
            Subject = "CDM Voting Two-Factor Authentication Code",
            Body = $"Your Authentication Code is: {code}",
            IsBodyHtml = false
        };

        var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new NetworkCredential(fromEmail, appPassword),
            EnableSsl = true,
            Timeout = 5000
        };

        smtp.Send(message);
    }
    public void SendPasswordResetEmail(string toEmail, string resetUrl)
    {
        var fromEmail = _config["Email:Sender"];
        var appPassword = _config["Email:AppPassword"];

        var message = new MailMessage(fromEmail, toEmail)
        {
            Subject = "CDM Voting System - Password Reset",
            Body = $@"
            <p>You requested to reset your password.</p>
            <p>Click the button below to set a new password:</p>
            <a href='{resetUrl}' 
               style='background-color:#1FA64A;color:white;padding:10px 20px;
                      text-decoration:none;border-radius:5px;display:inline-block;'>
                Reset Password
            </a>
            <p>This link will expire in 1 hour.</p>",
            IsBodyHtml = true
        };

        var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new NetworkCredential(fromEmail, appPassword),
            EnableSsl = true,
            Timeout = 5000
        };

        smtp.Send(message);
    }
}