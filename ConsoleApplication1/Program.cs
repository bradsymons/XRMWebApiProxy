using System;
using System.Web;
using System.Net.Http;
using System.Net.Http.Formatting;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using XRMWebApiWrapper;
using XRMWebApiWrapper.Microsoft.Dynamics.CRM;
using System.Reflection;
using System.Linq.Dynamic;
using System.Collections;
using System.Configuration;


namespace ConsoleApplication1
{

    class Program
    {
        public static int mainseed = 0;
        static void Main(string[] args)
        {


            if (true)
            {
                var orgService = new OrgService(Properties.Settings.Default.CRMUrl, Properties.Settings.Default.ApplicationId, Properties.Settings.Default.UserName, Properties.Settings.Default.Password);
                var cols = new ColumnSet();
                cols.Columns.Add("name");
                cols.Columns.Add("description");
                cols.Columns.Add("primarycontactid");
                cols.AllColumns = true;
                var response = orgService.RetrieveMultiple("accounts", cols);
                var accountList = JsonConvert.DeserializeObject<RetrieveMultipleResponse<Account>>(response).Value.ToList<Account>();


                foreach (var item in accountList)
                {
                    var retrieveResponse = orgService.Retrieve("accounts", item.Accountid.GetValueOrDefault(), cols);
                    Account acc = JsonConvert.DeserializeObject<Account>(retrieveResponse);
                    acc.Primarycontactid = new Contact
                    {
                        Contactid = new Guid("49A0E5B9-88DF-E311-B8E5-6C3BE5A8B200")
                    };
                    Console.WriteLine(acc.Name);
                    acc.Name = acc.Name.Substring(0, (acc.Name.Length > 11) ? acc.Name.Length - 10 : acc.Name.Length) + " " + DateTime.Now.ToShortTimeString();
                    orgService.Update("accounts", acc.Accountid.Value, acc, cols);
                    Console.WriteLine("Updated to: " + acc.Name);
                }



                //using (var client = new System.Net.Http.HttpClient())
                //{
                //    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //    client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                //    client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                //    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                //    var response = client.GetAsync("https://empiredps.api.crm6.dynamics.com/api/data/v8.1/accounts?$select=name&$top=3").Result;
                //    response.EnsureSuccessStatusCode();
                //    var Content = response.Content.ReadAsStringAsync().Result;
                //    ODataRetrieveMultipleResponse<account> odataresponse = JsonConvert.DeserializeObject<ODataRetrieveMultipleResponse<account>>(Content);

                //    for (int i = 0; i < odataresponse.Value.Length; i++)
                //    {
                //        using (var client2 = new System.Net.Http.HttpClient())
                //        {
                //            client2.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //            client2.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                //            client2.DefaultRequestHeaders.Add("OData-Version", "4.0");
                //            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                //            var response2 = client2.GetAsync("https://empiredps.api.crm6.dynamics.com/api/data/v8.1/accounts(" + odataresponse.Value[i].accountid.ToString() + ")").Result;
                //            response2.EnsureSuccessStatusCode();
                //            var accountContent = response2.Content.ReadAsStringAsync().Result;
                //            account acc = JsonConvert.DeserializeObject<account>(accountContent);
                //            Console.WriteLine(acc.name);
                //            var method = new HttpMethod("PATCH");
                //            using (var clientUpdate = new System.Net.Http.HttpClient())
                //            {


                //                var cols = new columnset();
                //                cols.columns.Add("name");
                //                cols.columns.Add("description");
                //                cols.columns.Add("ownerid");
                //                cols.allcolumns = false;
                //                acc.name = acc.name.Substring(0, 20) + DateTime.Now;

                //                var accUp = CreateUpdateObject(acc, cols);

                //                clientUpdate.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //                clientUpdate.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                //                clientUpdate.DefaultRequestHeaders.Add("OData-Version", "4.0");
                //                clientUpdate.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                //                HttpRequestMessage updateRequest = new HttpRequestMessage(method, "https://empiredps.api.crm6.dynamics.com/api/data/v8.1/accounts(" + acc.accountid.ToString() + ")");
                //                updateRequest.Content = new StringContent(JsonConvert.SerializeObject(accUp), System.Text.Encoding.UTF8, "application/json");
                //                var updateResp = clientUpdate.SendAsync(updateRequest).Result;
                //                updateResp.EnsureSuccessStatusCode();

                //            }
                //        }

                //    }
                //}
                Console.ReadLine();
            }
        }


        public static int CalculateWorkingDays(DateTime startDate, DateTime endDate)
        {
            int workingDays = 0;
            var currentDate = startDate.Kind == DateTimeKind.Utc ? startDate.ToLocalTime() : startDate;
            endDate = endDate.Kind == DateTimeKind.Utc ? endDate.ToLocalTime() : endDate;



            while (currentDate.Date < endDate.Date)
            {
                if (IsWorkingDay(currentDate))
                {
                    workingDays++;
                }
                currentDate = currentDate.AddDays(1);


            }

            return workingDays;
        }

        public static bool IsWorkingDay(DateTime date)
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                return true;
            return false;
        }





    }
}
