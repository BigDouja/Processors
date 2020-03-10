using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace FFToday.HTMLParser.Library
{
    public class Parser
    {
        public delegate void UpdateStatusHandler(string message);
        public event UpdateStatusHandler UpdateStatus;

        private void FireUpdateStatus(string message)
        {
            UpdateStatus?.Invoke(message);
        }

        public DataTable ParsePage(IEnumerable<FFTodayPosition> listOfPositions = null, int? season = null, int? week = null)
        {
            Regex regEx = new Regex(@".*\s*class[=\s].+(\w*tableclmhdr\w*|tablehdr)");
            Regex replaceSpc = new Regex(@"&nbsp;(\d*)\.?\s+");
            DataTable tableOfStats = new DataTable();

            if (!season.HasValue)
                season = DateTime.Now.Year - 1;

            tableOfStats.Columns.Add("Position", typeof(FFTodayPosition));
            tableOfStats.Columns.Add("Data", typeof(string));

            if (listOfPositions is null || listOfPositions.Count() == 0)
            {
                listOfPositions = new List<FFTodayPosition> { FFTodayPosition.DEF, FFTodayPosition.QB, FFTodayPosition.RB, FFTodayPosition.TE, FFTodayPosition.WR };
            }

            foreach (var position in listOfPositions)
            {
                var page = 0;

                FireUpdateStatus($"Loading data for {position.ToString()}.");
                
                while (true)
                {
                    List<string> listOfFound = new List<string>();
                    var data = string.Empty;
                    var gameWeek = week.HasValue ? week.ToString() : string.Empty;
                    var webLink = $"https://www.fftoday.com/stats/playerstats.php?Season={season}&GameWeek={gameWeek}&PosID={(int)position}&LeagueID=17&order_by=FFPts&sort_order=DESC&cur_page={page++}";

                    //extract data from page
                    using (var client = new WebClient())
                    {
                        data = client.DownloadString(webLink);
                    }

                    string htmlTag = string.Empty;
                    string tableData = string.Empty;

                    //find html tables of data
                    foreach (var c in data.ToCharArray())
                    {
                        if ((string.IsNullOrWhiteSpace(htmlTag) && c == '<') || (!string.IsNullOrWhiteSpace(htmlTag) && htmlTag.Last() != '>'))
                        {
                            htmlTag = c == '<' ? c.ToString() : htmlTag + c;
                        }
                        else if (htmlTag.StartsWith("<table", StringComparison.OrdinalIgnoreCase) && !tableData.EndsWith("</table>", StringComparison.OrdinalIgnoreCase))
                        {
                            tableData += (string.IsNullOrWhiteSpace(tableData) ? htmlTag : string.Empty) + c;
                        }
                        else if (tableData.EndsWith("</table>", StringComparison.OrdinalIgnoreCase))
                        {
                            //build inner table, YAGNI: So far we have only encountered inner tables, specifically we are not looking for a list of tables inside a table
                            var buildFoundTable = string.Empty;

                            foreach (var letter in tableData.ToCharArray().Reverse())
                            {
                                buildFoundTable = letter + buildFoundTable;

                                if (buildFoundTable.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
                                {
                                    listOfFound.Add(buildFoundTable);
                                    tableData = string.Empty;
                                    break;
                                }
                            }

                            htmlTag = c == '<' ? c.ToString() : string.Empty;
                        }
                        else if (!string.IsNullOrWhiteSpace(htmlTag) && !htmlTag.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
                        {
                            htmlTag = c == '<' ? c.ToString() : string.Empty;
                        }
                    }

                    //convert data into a DataTale
                    var playerTable = listOfFound[6];
                    List<string> breakDown = new List<string>();
                    var rowData = string.Empty;
                    
                    htmlTag = string.Empty;
                    tableData = string.Empty;

                    foreach (var c in playerTable.ToCharArray())
                    {
                        if ((string.IsNullOrWhiteSpace(htmlTag) && c == '<') || (!string.IsNullOrWhiteSpace(htmlTag) && htmlTag.Last() != '>'))
                        {
                            htmlTag = c == '<' ? c.ToString() : htmlTag + c;
                        }
                        else if (htmlTag.StartsWith("<table", StringComparison.OrdinalIgnoreCase) && !tableData.EndsWith("</table>", StringComparison.OrdinalIgnoreCase))
                        {
                            tableData += c;

                            if (tableData.IndexOf("<tr", StringComparison.OrdinalIgnoreCase) > -1 && tableData.EndsWith("</tr>", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!regEx.IsMatch(tableData.Substring(tableData.IndexOf("<tr", StringComparison.OrdinalIgnoreCase))))
                                {
                                    breakDown.Add(tableData.Substring(tableData.IndexOf("<tr", StringComparison.OrdinalIgnoreCase)));

                                    var row = tableOfStats.NewRow();
                                    var dataStr = breakDown.Last().Replace("\n", string.Empty).Replace("\r", string.Empty);
                                    var match = replaceSpc.Match(dataStr);
                                    dataStr = replaceSpc.Replace(dataStr, $"<RANK>{replaceSpc.Match(dataStr).Groups[1].Value}</RANK>");

                                    row["Position"] = position;
                                    row["Data"] = dataStr;

                                    tableOfStats.Rows.Add(row);
                                }

                                tableData = string.Empty;
                            }
                        }
                        else if (tableData.EndsWith("</table>", StringComparison.OrdinalIgnoreCase))
                        {
                            htmlTag = string.Empty;
                        }
                    }

                    if (breakDown.Count == 0)
                    {
                        tableOfStats.AcceptChanges();

                        break;
                    }
                }
            }

            return tableOfStats;
        }
    } 
}
