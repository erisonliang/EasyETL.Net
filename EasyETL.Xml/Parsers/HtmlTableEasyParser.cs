﻿using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace EasyETL.Xml.Parsers
{
    [DisplayName("HtmlTable")]
    public class HtmlTableEasyParser : MultipleLineEasyParser
    {
        public override XmlDocument Load(TextReader txtReader, XmlDocument xDoc = null)
        {
            #region setup the rootNode
            XmlNode rootNode;
            if (xDoc == null)
            {
                xDoc = new XmlDocument();
            }
            if (xDoc.DocumentElement == null)
            {
                rootNode = xDoc.CreateElement(RootNodeName);
                xDoc.AppendChild(rootNode);
            }

            rootNode = xDoc.DocumentElement;
            #endregion

            string HTML = txtReader.ReadToEnd();
            ConvertHTMLTablesXml(HTML, xDoc, rootNode);

            return xDoc;
        }

        private void ConvertHTMLTablesXml(string HTML, XmlDocument xDoc, XmlNode rootNode)
        {
            string TableExpression = "<TABLE[^>]*>(.*?)</TABLE>";
            string HeadRowExpression = "(<THEAD>|<THEAD[\\s]>)(.*?)</THEAD>";
            string HeaderExpression = "(<TH>|<TH[\\s]>)(.*?)</TH>";
            string RowExpression = "(<TR>|<TR[\\s]>)(.*?)</TR>";
            string ColumnExpression = "(<TD>|<TD[\\s]>)(.*?)</TD>";
            // Get a match for all the tables in the HTML  
            MatchCollection Tables = Regex.Matches(HTML, TableExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // Loop through each table element  
            int tableIndex = 0;
            foreach (Match Table in Tables)
            {
                List<string> lstFields = new List<string>();
                tableIndex++;
                string tableName = RootNodeName + tableIndex.ToString();
                // Reset the current row counter and the header flag  
                int iCurrentRow = 0;
                bool HeadersExist = false;
                Match TableNameMatch = null;
                if (Regex.IsMatch(Table.Value, "id=(?<TableName>.\\w+)")) TableNameMatch = Regex.Match(Table.Value, "id=(?<TableName>.\\w+)");
                if (Regex.IsMatch(Table.Value, "name=(?<TableName>.\\w+)")) TableNameMatch = Regex.Match(Table.Value, "name=(?<TableName>.\\w+)");

                if (TableNameMatch != null)
                    tableName = TableNameMatch.Groups["TableName"].ToString().Trim('"');

                if (Table.Value.IndexOf("<TH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Set the HeadersExist flag  
                    HeadersExist = !Regex.IsMatch(Table.Value, HeadRowExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    // Get a match for all the rows in the table  
                    MatchCollection Headers = Regex.Matches(Table.Value, HeaderExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    // Loop through each header element  
                    foreach (Match Header in Headers)
                    {

                        if (!lstFields.Contains(Header.Groups[2].ToString()))
                        {
                            lstFields.Add(Header.Groups[2].ToString());
                        }
                    }
                }
                else
                {
                    MatchCollection tableCollection = Regex.Matches(Table.Value, TableExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    MatchCollection rowCollection = (tableCollection.Count > 0) ? Regex.Matches(tableCollection[0].ToString(), RowExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase) : null;
                    MatchCollection colCollection = (rowCollection.Count > 0) ? Regex.Matches(rowCollection[0].ToString(), ColumnExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase) : null;
                    if (colCollection.Count > 0)
                    {
                        for (int iColumns = 1; iColumns <= colCollection.Count; iColumns++)
                        {
                            lstFields.Add("Column" + iColumns);
                        }
                    }
                }

                SetFieldNames(lstFields.ToArray());

                //Get a match for all the rows in the table  
                MatchCollection Rows = Regex.Matches(Table.Value, RowExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                // Loop through each row element 
                int errorCount = 0;
                int rowCount = 0;
                foreach (Match Row in Rows)
                {
                    // Only loop through the row if it isn't a header row  
                    if (!(iCurrentRow == 0 && HeadersExist))
                    {
                        try
                        {
                            // Create a new row and reset the current column counter  
                            XmlNode rowNode = rootNode.OwnerDocument.CreateElement(tableName);
                            //rootNode.AppendChild(rowNode);

                            int iCurrentColumn = 0;
                            // Get a match for all the columns in the row  
                            MatchCollection Columns = Regex.Matches(Row.Value, ColumnExpression, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);

                            if (lstFields.Count < Columns.Count)
                            {
                                while (lstFields.Count < Columns.Count)
                                {
                                    lstFields.Add("Column" + (lstFields.Count + 1));
                                }
                                SetFieldNames(lstFields.ToArray());
                            }


                            // Loop through each column element  
                            foreach (Match Column in Columns)
                            {
                                XmlNode columnNode = rowNode.OwnerDocument.CreateElement(XmlConvert.EncodeName(FieldNames[iCurrentColumn]));
                                if (Column.Groups.Count > 2) columnNode.InnerText = Column.Groups[2].ToString();
                                rowNode.AppendChild(columnNode);
                                iCurrentColumn++;
                            }

                            AddRow(xDoc, rowNode);

                            if ((rowNode != null) && (rowNode.HasChildNodes))
                            {
                                rootNode.AppendChild(rowNode);
                                rowCount++;
                                if (rowCount >= MaxRecords) return;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            RaiseException(new MalformedLineException(Row.Value, iCurrentRow, ex));
                            if (errorCount > MaximumErrorsToAbort) return;
                        }
                    }
                    // Increase the current row counter  
                    iCurrentRow++;
                }
            }
        }

        public override Dictionary<string, string> GetSettingsAsDictionary()
        {
            Dictionary<string, string> resultDict = base.GetSettingsAsDictionary();
            resultDict["parsertype"] = "HtmlTable";
            return resultDict;
        }
    }
}
