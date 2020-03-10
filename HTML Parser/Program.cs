using Dapper;
using FFToday.HTMLParser.Library;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace HTML_Parser
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                for (int year = 2015; year < DateTime.Now.Year; year++)
                {
                    for (int week = 0; week <= 17; week++)
                    {
                        if (week == 0)
                            Console.WriteLine($"{Environment.NewLine}Retrieving Data for {year.ToString()}...");
                        else
                            Console.WriteLine($"{Environment.NewLine}Retrieving Data for {year.ToString()} - Week #{week}...");

                        var parser = new Parser();
                        parser.UpdateStatus += Parser_UpdateStatus;
                        int? weekVar = null;

                        if (week > 0)
                            weekVar = week;

                        var dataTable = parser.ParsePage(new List<FFTodayPosition> { FFTodayPosition.QB, FFTodayPosition.RB, FFTodayPosition.WR, FFTodayPosition.TE }, year, weekVar);
                        XElement playerData = new XElement("PlayerData");

                        dataTable.Rows.Cast<DataRow>().ToList().ForEach(row =>
                        {
                            playerData.Add(new XElement("Player", new object[]
                                {
                                    new XAttribute("Position", ((FFTodayPosition)int.Parse(row[0].ToString())).ToString()),
                                    XElement.Parse(row[1].ToString()).Elements()
                                }));
                        });

                        using (var sw = new StreamWriter($"data_{year.ToString()}.txt"))
                        {
                            playerData.Save(sw);

                            sw.Flush();
                            sw.Close();
                        }

                        Console.WriteLine($"{Environment.NewLine}Loading Data for {year.ToString()}...");

                        using (var dbConn = new SqlConnection(ConfigurationManager.ConnectionStrings["FantasyFootball"].ConnectionString))
                        {
                            dbConn.Open();
                            var transaction = dbConn.BeginTransaction();

                            try
                            {
                                playerData.Descendants("Player").ToList().ForEach(elem =>
                                {
                                    XElement[] xElements = elem.Elements("TD").ToArray();
                                    int position = 0;
                                    string playerName = xElements[0].Element("A").Value;
                                    string webLink = $"https://fftoday.com{xElements[0].Element("A").Attribute("HREF").Value}";
                                    int playerRank = int.Parse(xElements[0].Element("RANK").Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands);
                                    string teamName = xElements[1].Value;
                                    int games = int.Parse(xElements[2].Value);
                                    float totPts = 0f;
                                    int? passYds = null;
                                    int? passTds = null;
                                    int? passInt = null;
                                    int? rushAtt = null;
                                    int? rushYds = null;
                                    int? rushTds = null;
                                    int? recTgt = null;
                                    int? rec = null;
                                    int? recYds = null;
                                    int? recTds = null;

                                    Console.WriteLine($"Processing {playerName}...");

                                    if (elem.Attribute("Position").Value == FFTodayPosition.QB.ToString())
                                    {
                                        position = 1;
                                        passYds = int.Parse(xElements[5].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        passTds = int.Parse(xElements[6].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        passInt = int.Parse(xElements[7].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushAtt = int.Parse(xElements[8].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushYds = int.Parse(xElements[9].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushTds = int.Parse(xElements[10].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        totPts = float.Parse(xElements[11].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                    }
                                    else if (elem.Attribute("Position").Value == FFTodayPosition.RB.ToString())
                                    {
                                        position = 2;
                                        rushAtt = int.Parse(xElements[3].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushYds = int.Parse(xElements[4].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushTds = int.Parse(xElements[5].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recTgt = int.Parse(xElements[6].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rec = int.Parse(xElements[7].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recYds = int.Parse(xElements[8].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recTds = int.Parse(xElements[9].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        totPts = float.Parse(xElements[10].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                    }
                                    else if (elem.Attribute("Position").Value == FFTodayPosition.WR.ToString())
                                    {
                                        position = 3;
                                        recTgt = int.Parse(xElements[3].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rec = int.Parse(xElements[4].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recYds = int.Parse(xElements[5].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recTds = int.Parse(xElements[6].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushAtt = int.Parse(xElements[7].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushYds = int.Parse(xElements[8].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rushTds = int.Parse(xElements[9].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        totPts = float.Parse(xElements[10].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                    }
                                    else if (elem.Attribute("Position").Value == FFTodayPosition.TE.ToString())
                                    {
                                        position = 4;
                                        recTgt = int.Parse(xElements[3].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        rec = int.Parse(xElements[4].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recYds = int.Parse(xElements[5].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        recTds = int.Parse(xElements[6].Value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);
                                        totPts = float.Parse(xElements[7].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                    }

                                // upsert Team
                                var param = new DynamicParameters();

                                    param.Add("@teamName", teamName, DbType.String, ParameterDirection.Input);
                                    param.Add("@id", null, DbType.Int32, ParameterDirection.Output);

                                    dbConn.Execute("upsertTeam", param: param, transaction: transaction, commandType: CommandType.StoredProcedure);

                                    int teamId = param.Get<int>("@id");

                                // upsert Player
                                param = new DynamicParameters();

                                    param.Add("@positionId", position);
                                    param.Add("@teamId", teamId);
                                    param.Add("@name", playerName);
                                    param.Add("@active", true);
                                    param.Add("@webLink", webLink);
                                    param.Add("@id", null, DbType.Int32, direction: ParameterDirection.Output);

                                    dbConn.Execute("upsertPlayer", param: param, transaction: transaction, commandType: CommandType.StoredProcedure);

                                    int playerId = param.Get<int>("@id");

                                // upsert Player Statistics
                                param = new DynamicParameters();

                                    param.Add("@PlayerID", playerId);
                                    param.Add("@Year", year);
                                    if (week > 0)
                                        param.Add("@Week", week);
                                    if (week == 0)
                                        param.Add("@GamesPlayed", games);
                                    param.Add("@FanPts", totPts);
                                    param.Add("@Actual", playerRank);
                                    param.Add("@PassYds", passYds);
                                    param.Add("@PassTD", passTds);
                                    param.Add("@Int", passInt);
                                    param.Add("@RushAtt", rushAtt);
                                    param.Add("@RushYds", rushYds);
                                    param.Add("@RushTD", rushTds);
                                    param.Add("@RecTgt", recTgt);
                                    param.Add("@Rec", rec);
                                    param.Add("@RecYds", recYds);
                                    param.Add("@RecTD", recTds);
                                    param.Add("@statsId", null, DbType.Int32, direction: ParameterDirection.Output);

                                    dbConn.Execute(week == 0 ? "upsertYahooStats" : "upsertYahooWeeklyStats", param: param, transaction: transaction, commandType: CommandType.StoredProcedure);

                                    int statsId = param.Get<int>("@statsId");
                                });

                                transaction.Commit();
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();

                                throw;
                            }
                            finally
                            {
                                dbConn.Close();
                            }
                        }
                    }
                }

                Console.WriteLine($"{Environment.NewLine}Done.");
            }
            catch (Exception exc)
            {
                Console.WriteLine();
                Console.WriteLine($"***********************************{Environment.NewLine}Execption:{Environment.NewLine}{exc.ToString()}{Environment.NewLine}***********************************");
                Console.ReadLine();
            }
        }

        private static void Parser_UpdateStatus(string message)
        {
            Console.WriteLine(message);
        }
    }
}
