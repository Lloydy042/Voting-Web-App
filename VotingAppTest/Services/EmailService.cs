using System;
using System.Net;
using System.Net.Mail;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class EmailService
{
    private readonly IConfiguration _config;
    private static readonly HttpClient _httpClient = new HttpClient();

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    private void SendEmail(string toEmail, string subject, string body, bool isHtml)
    {
        var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? _config["Email:ResendApiKey"];

        if (!string.IsNullOrEmpty(resendApiKey))
        {
            try
            {
                SendViaResend(resendApiKey, toEmail, subject, body, isHtml);
                return;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to send email via Resend API: " + ex.Message, ex);
            }
        }

        // Fall back to SMTP
        SendViaSmtp(toEmail, subject, body, isHtml);
    }

    private void SendViaResend(string apiKey, string toEmail, string subject, string body, bool isHtml)
    {
        var fromEmail = Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL") 
                        ?? _config["Email:ResendFrom"] 
                        ?? "onboarding@resend.dev";

        var payload = new
        {
            from = fromEmail,
            to = new[] { toEmail },
            subject = subject,
            html = isHtml ? body : null,
            text = isHtml ? null : body
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails"))
        {
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = content;

            var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = response.Content.ReadAsStringAsync().Result;
                throw new Exception($"Resend API returned status code {response.StatusCode}: {errorText}");
            }
        }
    }

    private void SendViaSmtp(string toEmail, string subject, string body, bool isHtml)
    {
        var fromEmail = _config["Email:Sender"];
        var appPassword = _config["Email:AppPassword"];

        var message = new MailMessage(fromEmail, toEmail)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        using (var smtp = new SmtpClient("smtp.gmail.com", 587))
        {
            smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
            smtp.EnableSsl = true;
            smtp.Timeout = 5000;
            smtp.Send(message);
        }
    }

    public void SendVerificationEmail(string toEmail, string verifyUrl)
    {
        var body = $@"
            <p>Welcome to CDM Voting!</p>
            <p>Click the button below to verify your account:</p>
            <a href='{verifyUrl}' 
               style='background-color:#4CAF50;color:white;padding:10px 20px;
                      text-decoration:none;border-radius:5px;display:inline-block;'>
                Verify Account
            </a>";
        SendEmail(toEmail, "CDM Voting Account Verification", body, true);
    }

    public void SendAuthenticationCode(string toEmail, string code)
    {
        var body = $"Your Authentication Code is: {code}";
        SendEmail(toEmail, "CDM Voting Two-Factor Authentication Code", body, false);
    }

    public void SendPasswordResetEmail(string toEmail, string resetUrl)
    {
        var body = $@"
            <p>You requested to reset your password.</p>
            <p>Click the button below to set a new password:</p>
            <a href='{resetUrl}' 
               style='background-color:#1FA64A;color:white;padding:10px 20px;
                      text-decoration:none;border-radius:5px;display:inline-block;'>
                Reset Password
            </a>
            <p>This link will expire in 1 hour.</p>";
        SendEmail(toEmail, "CDM Voting System - Password Reset", body, true);
    }
}