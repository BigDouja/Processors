using Dapper;
using Newtonsoft.Json;
using RatliffFamily.DTO;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Emailer
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                bool stop = false;
                bool stopped = true;

                Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

                Task emailEngine = Task.Run(() =>
                {
                    try
                    {
                        stopped = false;

                        while (!stop)
                        {
                            IEnumerable<EmailRequest> requests = null;

                            foreach (var dbCfgConn in ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().Skip(1))
                            {
                                using (var dbConn = new SqlConnection(dbCfgConn.ConnectionString))
                                {
                                    Console.WriteLine($"Gathering data from {dbConn.DataSource}.{dbConn.Database}....");

                                    if (requests == null)
                                        requests = dbConn.Query<EmailRequest>(sql: "GetEmailRequests", commandType: CommandType.StoredProcedure);
                                    else
                                        requests.Concat(dbConn.Query<EmailRequest>(sql: "GetEmailRequests", commandType: CommandType.StoredProcedure));
                                }
                            }

                            foreach (var emailReq in requests)
                            {
                                Console.WriteLine($"Building Email Request #{emailReq.Id}.......");

                                var toAddress = new EmailAddress(emailReq.ToAddress);
                                var fromAddress = new EmailAddress(emailReq.FromAddress);
                                var subject = emailReq.Subject;
                                var plainTextContent = string.Empty;
                                var htmlContent = emailReq.HtmlBody;

                                var msg = MailHelper.CreateSingleEmail(fromAddress, toAddress, subject, plainTextContent, htmlContent);

                                Console.WriteLine($"Sending Email Request #{emailReq.Id} from {emailReq.FromAddress} to {emailReq.ToAddress}.");

                                bool sent = false;
                                string statusMessage = null;

                                try
                                {
                                    var response = SendEmail(msg).Result;

                                    sent = true;

                                    if (response.StatusCode != System.Net.HttpStatusCode.Accepted && response.StatusCode != System.Net.HttpStatusCode.OK)
                                    {
                                        string jsonStr = response.Body.ReadAsStringAsync().Result;
                                        var resObj = JsonConvert.DeserializeObject(jsonStr);

                                        statusMessage = $"ERROR - {{{response.StatusCode}}}: {resObj.ToString()}";

                                        Console.WriteLine($"Error sending email request #{emailReq.Id}.");
                                        Console.WriteLine($"Status --> {statusMessage}");
                                    }
                                    else
                                        Console.WriteLine($"Email request #{emailReq.Id} has been sent.");
                                }
                                finally
                                {
                                    using (var dbConn = new SqlConnection(ConfigurationManager.ConnectionStrings["Default"].ConnectionString))
                                    {
                                        var paramters = new DynamicParameters();

                                        paramters.Add("@sent", sent, DbType.Boolean, ParameterDirection.Input);
                                        paramters.Add("@id", emailReq.Id, DbType.Int32, ParameterDirection.Input);
                                        paramters.Add("@status_message", statusMessage, DbType.String, ParameterDirection.Input);

                                        dbConn.Execute(sql: "UpsertEmailRequest", param: paramters, commandType: CommandType.StoredProcedure);
                                    }
                                }

                                if (stop)
                                    break;
                            }

                            Console.WriteLine($"Sleeping {{{DateTime.Now.ToLongTimeString()}}}....");

                            DateTime end = DateTime.Now.AddMilliseconds(TimeSpan.FromMilliseconds(60000).TotalMilliseconds);

                            while (DateTime.Now < end && !stop)
                            {
                                Thread.Sleep(500);
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("----------------------------------------");
                        Console.WriteLine("Fatal Exception - Shut down main thread.");
                        Console.WriteLine("----------------------------------------");
                        Console.WriteLine(exc.Message);
                        Console.WriteLine("----------------------------------------");
                    }
                    finally
                    {
                        stopped = true;
                    }
                });

                Console.ReadLine();

                stop = true;

                while (!stopped)
                    Thread.Sleep(100);

                Console.WriteLine("The processor is done.");
            }
            catch (Exception exc)
            {
                Console.WriteLine();
                Console.WriteLine("*********************************");
                Console.WriteLine(exc.Message);
                Console.WriteLine();
                Console.WriteLine(exc.ToString());
                Console.WriteLine("*********************************");
            }
            finally
            {
                Console.WriteLine();
                Console.Write("Done.  Presee <Enter> to close the application.");
                Console.ReadLine();
            }
        }

        static async Task<Response> SendEmail(SendGridMessage msg)
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY", EnvironmentVariableTarget.User);

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ApplicationException("no api key detected");

            var client = new SendGridClient(apiKey);
            var response = await client.SendEmailAsync(msg);

            return response;
        }
    }
}
