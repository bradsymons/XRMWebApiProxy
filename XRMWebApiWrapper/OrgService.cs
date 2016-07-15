using System;
using System.Web;
using System.Net.Http;
using System.Net.Http.Formatting;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;
using System.Linq.Dynamic;
using System.Collections;
using System.Dynamic;
using XRMWebApiWrapper.Microsoft.Dynamics.CRM;


namespace XRMWebApiWrapper
{
    //TODO: move these out to a seperate file in models
    public class CRMEntityObject<T>
    {
        T crmObject { get; set; }
    }

    public class EntityReference
    {
        public EntityReference(string EntityLogicalName, Guid Id)
        {
            this.Id = Id;
            this.LogicalName = EntityLogicalName;
        }
        public Guid Id { get; set; }
        public string LogicalName { get; set; }
    }

    public class RetrieveMultipleResponse<T>
    {
        public T[] Value { get; set; }

    }

    public class CRMLookup
    {
        public string OldName { get; set; }
        public string NewName { get; set; }
        public string Value { get; set; }

        public string PrimaryFiledName { get; set; }
    }

    public class FieldListItem
    {
        public string name { get; set; }
        public Type type { get; set; }
        public string objectName { get; set; }

        public PropertyInfo propertyInfo { get; set; }
    }

    public class OrgService
    {
        private string url;
        private string clientAppId;
        private string username;
        private string password;
        private string connectionToken;
        private DateTimeOffset connectionTokenExpiry;


        public OrgService(string crmUrl, string appId, string username, string password)
        {
            this.url = crmUrl;
            this.clientAppId = appId;
            this.username = username;
            this.password = password;
        }

        private void GetToken()
        {
            //If we already have a valid token, don't get a new one.
            //We increment now by a few seconds to make sure when we submit our request we are still inside the expiry date
            if (connectionTokenExpiry != null && DateTime.Now.AddSeconds(5) < connectionTokenExpiry && !String.IsNullOrEmpty(connectionToken))
                return;

            try
            {
                AuthenticationContext authContext = new AuthenticationContext("https://login.windows.net/common", false);
                AuthenticationResult result = authContext.AcquireToken(url, clientAppId, new UserCredential(username, password));
                connectionToken = result.AccessToken;
                connectionTokenExpiry = result.ExpiresOn.ToLocalTime();
            }
            catch (Exception ex)
            {
                //TODO: make some nice friendly error messages based on some typical things that can go wrong.
                throw ex;
            }

        }

        //TODO: Need to figure out how we can pass back an actual object to the caller, not the json.
        public string RetrieveMultiple(string EntityPluralLogicalName, ColumnSet columnSet)
        {
            GetToken();
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionToken);
                var selectStatement = CreateSelectStatement(columnSet);

                var response = client.GetAsync(url + "/api/data/v8.1/" + EntityPluralLogicalName + "" + selectStatement).Result;
                response.EnsureSuccessStatusCode();
                var Content = response.Content.ReadAsStringAsync().Result;

                return Content;
            }
        }

        public string Retrieve(string EntityPluralLogicalName, Guid id, ColumnSet columnSet)
        {
            GetToken();
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionToken);
                var selectStatement = CreateSelectStatement(columnSet);
                var response = client.GetAsync(url + "/api/data/v8.1/" + EntityPluralLogicalName + "(" + id.ToString() + ")" + selectStatement).Result;
                response.EnsureSuccessStatusCode();
                var Content = response.Content.ReadAsStringAsync().Result;
                return Content;
            }
        }


        public void Update(string EntityPluralLogicalName, Guid id, object entity, ColumnSet columnSet)
        {
            GetToken();
            var updateEntity = CreateUpdateObject(entity, columnSet);


            var method = new HttpMethod("PATCH");
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connectionToken);

                HttpRequestMessage updateRequest = new HttpRequestMessage(method, url + "/api/data/v8.1/accounts(" + id.ToString() + ")");

                //var test = @"{""description"": ""desc"",""name"": ""A. Datum Updated Upd14/07/2 9:59 AM"",""primarycontactid"": { ""Id"": ""49a0e5b9-88df-e311-b8e5-6c3be5a8b200"",""LogicalName"": ""contact"" } }";
                var test = @"{""description"": ""desc"",""name"": ""A. Datum Updated Upd14/07/2 9:59 AM"",""primarycontactid@odata.bind"": ""/contacts(49a0e5b9-88df-e311-b8e5-6c3be5a8b200)"" }";
                updateRequest.Content = new StringContent(updateEntity, System.Text.Encoding.UTF8, "application/json");
                //updateRequest.Content = new StringContent(updateEntity, System.Text.Encoding.UTF8, "application/json");

                var response = client.SendAsync(updateRequest).Result;
                response.EnsureSuccessStatusCode();
            }
        }
        private string CreateUpdateObject(object entity, ColumnSet cols)
        {
            //We need to remove all the properties in the entity definition that cause issues when we submit the json object to the webapi
            //So far I have found that any Microsoft.OData.Client.DataServiceCollection type (relationships and anything that starts with an underscore(value fields) cause problems.
            //There might be a much better way of doing this???
            //TODO: figure out how to get the guid of the related entity


            Type type = entity.GetType();
            Type listType = typeof(List<>).MakeGenericType(new[] { type });
            IList tempList = (IList)Activator.CreateInstance(listType);
            var actualEntity = Convert.ChangeType(entity, type);
            tempList.Add(actualEntity);

            var entityAttributes = entity.GetType().GetProperties();
            var entityAttributesList = entityAttributes.ToList();
            var columnList = cols.Columns.ToList();
            List<CRMLookup> lookupsToProcess = new List<CRMLookup>();
            String select = "new(";
            bool first = true;
            IEnumerable<FieldListItem> listToProcess = null;
            if (!cols.AllColumns.GetValueOrDefault())
            {
                listToProcess = (from colset in columnList
                                 join fieldSet in entityAttributesList on colset.ToLower() equals fieldSet.Name.ToLower()
                                 where !fieldSet.PropertyType.FullName.Contains("Microsoft.OData.Client.DataServiceCollection")
                                 && !fieldSet.Name.StartsWith("_")
                                 select new FieldListItem
                                 {
                                     name = fieldSet.Name,
                                     type = fieldSet.PropertyType,
                                     objectName = fieldSet.ToString()
                                 });


            }
            else
            {
                listToProcess = (from fieldSet in entityAttributesList
                                 where !fieldSet.PropertyType.FullName.Contains("Microsoft.OData.Client.DataServiceCollection")
                                 && !fieldSet.Name.StartsWith("_")
                                 select new FieldListItem
                                 {
                                     name = fieldSet.Name,
                                     type = fieldSet.PropertyType,
                                     objectName = fieldSet.ToString()
                                 });
            }

            var lookupList = (from field in listToProcess
                              where field.type.FullName.Contains("Microsoft.Dynamics.CRM")
                              //TODO Remove this once we know how to ge the guid from the actual entity we have.
                              && field.type.FullName.Contains("Microsoft.Dynamics.CRM.Contact")
                              select new
                              {
                                  name = field.name,
                                  type = field.type,
                                  objectName = field.objectName
                              });
            foreach (var lookup in lookupList)
            {
                var value = tempList
                            .Select("new (" + lookup.name + ")")
                            .Where("" + lookup.name + " != null");
                if (value.Count() > 0)
                {
                    var start = lookup.type.FullName.IndexOf("Microsoft.Dynamics.CRM") + "Microsoft.Dynamics.CRM".Length + 1;
                    var numChars = lookup.type.FullName.Length - lookup.type.FullName.IndexOf("Microsoft.Dynamics.CRM") - +"Microsoft.Dynamics.CRM".Length - 1;
                    var related = lookup.type.FullName.Substring(start, numChars);

                    var valueEnumer = value.GetEnumerator();

                    if (valueEnumer.MoveNext())
                    {
                        //TODO Here I need to actually get the guid from the related entity... not sure how yet :(
                        var guidStr = "<ReplaceWithGuid>";
                        var crmLookup = new CRMLookup
                        {
                            OldName = lookup.name,
                            NewName = lookup.name + "@odata.bind",
                            Value = String.Format(@"/{0}s({1})", related.ToLower(), guidStr),
                            PrimaryFiledName = related + "id"
                        };
                        lookupsToProcess.Add(crmLookup);
                    }
                }
            }
            foreach (var item in listToProcess)
            {
                if (first)
                    select += item.name + @" as " + item.name.ToLower() + @"";
                else
                    select += "," + item.name + @" as " + item.name.ToLower() + @"";
                first = false;
            }

            select += ")";

            var newobj = tempList
                .Select(select);
            var ObjEnumer = newobj.GetEnumerator();
            if (ObjEnumer.MoveNext())
            {
                var obj = ObjEnumer.Current;
                JObject jO = JObject.Parse(JsonConvert.SerializeObject(obj));
                foreach (var item in lookupsToProcess)
                {
                    var oldLookup = jO["primarycontactid"];
                    var guidToken = (JProperty)oldLookup.First(x => x.Type == JTokenType.Property && ((JProperty)x).Name == item.PrimaryFiledName);
                    var guidValue = guidToken.Value.ToString().Replace("{","").Replace("}","");
                    ((JProperty)jO.Descendants().Where(x => x.Type == JTokenType.Property && ((JProperty)x).Name == item.OldName.ToLower()).FirstOrDefault()).Remove();
                    //JObject entityRefJobj = JObject.Parse(JsonConvert.SerializeObject(item.Value));
                    jO.Add(item.NewName.ToLower(), item.Value.Replace("<ReplaceWithGuid>",guidValue));
                }
                return jO.ToString();
            }
            return null;
        }

        private string CreateSelectStatement(ColumnSet cols)
        {
            string select = "?$select";

            if (cols.AllColumns.GetValueOrDefault())
            {
                return "";
            }
            else
            {
                bool first = true;
                foreach (var item in cols.Columns)
                {
                    if (first)
                        select += "=" + item.ToLower();
                    else
                        select += "," + item.ToLower();

                    first = false;
                }
            }
            return select;
        }
    }
}
