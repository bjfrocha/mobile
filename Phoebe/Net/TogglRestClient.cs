using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    public class TogglRestClient : ITogglClient
    {
        private static readonly DateTime UnixStart = new DateTime (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly Uri v8Url;
        private readonly HttpClient httpClient;

        public TogglRestClient (Uri url)
        {
            v8Url = new Uri (url, "v8/");
            httpClient = new HttpClient ();
            var headers = httpClient.DefaultRequestHeaders;
            headers.UserAgent.Clear ();
            headers.UserAgent.Add (new ProductInfoHeaderValue (Platform.AppIdentifier, Platform.AppVersion));
            headers.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
        }

        public async Task Create<T> (T model)
            where T : Model
        {
            var type = model.GetType ();
            if (type == typeof(ClientModel)) {
                await CreateClient (model as ClientModel);
            } else if (type == typeof(ProjectModel)) {
                await CreateProject (model as ProjectModel);
            } else if (type == typeof(TaskModel)) {
                await CreateTask (model as TaskModel);
            } else if (type == typeof(TimeEntryModel)) {
                await CreateTimeEntry (model as TimeEntryModel);
            } else if (type == typeof(WorkspaceModel)) {
                await CreateWorkspace (model as WorkspaceModel);
            } else if (type == typeof(UserModel)) {
                await CreateUser (model as UserModel);
            } else {
                throw new NotSupportedException ("Creating of model (of type T) is not supported.");
            }
        }

        public async Task<T> Get<T> (long id)
            where T : Model
        {
            if (typeof(T) == typeof(ClientModel)) {
                return await GetClient (id) as T;
            } else if (typeof(T) == typeof(ProjectModel)) {
                return await GetProject (id) as T;
            } else if (typeof(T) == typeof(TaskModel)) {
                return await GetTask (id) as T;
            } else if (typeof(T) == typeof(TimeEntryModel)) {
                return await GetTimeEntry (id) as T;
            } else if (typeof(T) == typeof(WorkspaceModel)) {
                return await GetWorkspace (id) as T;
            } else if (typeof(T) == typeof(UserModel)) {
                return await GetUser (id) as T;
            } else {
                throw new NotSupportedException ("Fetching of model (of type T) is not supported.");
            }
        }

        public async Task<List<T>> List<T> ()
            where T : Model
        {
            if (typeof(T) == typeof(ClientModel)) {
                return await ListClients () as List<T>;
            } else if (typeof(T) == typeof(TimeEntryModel)) {
                return await ListTimeEntries () as List<T>;
            } else if (typeof(T) == typeof(WorkspaceModel)) {
                return await ListWorkspaces () as List<T>;
            } else {
                throw new NotSupportedException ("Listing of models (of type T) is not supported.");
            }
        }

        public async Task Update<T> (T model)
            where T : Model
        {
            var type = model.GetType ();
            if (type == typeof(ClientModel)) {
                await UpdateClient (model as ClientModel);
            } else if (type == typeof(ProjectModel)) {
                await UpdateProject (model as ProjectModel);
            } else if (type == typeof(TaskModel)) {
                await UpdateTask (model as TaskModel);
            } else if (type == typeof(TimeEntryModel)) {
                await UpdateTimeEntry (model as TimeEntryModel);
            } else if (type == typeof(WorkspaceModel)) {
                await UpdateWorkspace (model as WorkspaceModel);
            } else if (type == typeof(UserModel)) {
                await UpdateUser (model as UserModel);
            } else {
                throw new NotSupportedException ("Updating of model (of type T) is not supported.");
            }
        }

        public async Task Delete<T> (T model)
            where T : Model
        {
            var type = model.GetType ();
            if (type == typeof(ClientModel)) {
                await DeleteClient (model as ClientModel);
            } else if (type == typeof(ProjectModel)) {
                await DeleteProject (model as ProjectModel);
            } else if (type == typeof(TaskModel)) {
                await DeleteTask (model as TaskModel);
            } else if (type == typeof(TimeEntryModel)) {
                await DeleteTimeEntry (model as TimeEntryModel);
            } else {
                throw new NotSupportedException ("Deleting of model (of type T) is not supported.");
            }
        }

        public async Task Delete<T> (IEnumerable<T> models)
            where T : Model
        {
            if (typeof(T) == typeof(ClientModel)) {
                await Task.WhenAll (models.Select ((model) => DeleteClient (model as ClientModel)));
            } else if (typeof(T) == typeof(ProjectModel)) {
                await DeleteProjects (models as IEnumerable<ProjectModel>);
            } else if (typeof(T) == typeof(TaskModel)) {
                await DeleteTasks (models as IEnumerable<TaskModel>);
            } else if (typeof(T) == typeof(TimeEntryModel)) {
                await Task.WhenAll (models.Select ((model) => DeleteTimeEntry (model as TimeEntryModel)));
            } else if (typeof(T) == typeof(Model)) {
                await Task.WhenAll (models.Select ((model) => Delete (model)));
            } else {
                throw new NotSupportedException ("Deleting of models (of type T) is not supported.");
            }
        }

        private string ModelToJson<T> (T model)
            where T : Model
        {
            string dataKey;
            if (typeof(T) == typeof(TimeEntryModel)) {
                dataKey = "time_entry";
            } else if (typeof(T) == typeof(ProjectModel)) {
                dataKey = "project";
            } else if (typeof(T) == typeof(ClientModel)) {
                dataKey = "client";
            } else if (typeof(T) == typeof(TaskModel)) {
                dataKey = "task";
            } else if (typeof(T) == typeof(WorkspaceModel)) {
                dataKey = "workspace";
            } else if (typeof(T) == typeof(UserModel)) {
                dataKey = "user";
            } else {
                throw new ArgumentException ("Don't know how to handle model of type T.", "model");
            }

            var json = new JObject ();
            json.Add (dataKey, JObject.FromObject (model));
            return json.ToString (Formatting.None);
        }

        private HttpRequestMessage SetupRequest (HttpRequestMessage req)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.Token != null) {
                req.Headers.Authorization = new AuthenticationHeaderValue ("Basic",
                    Convert.ToBase64String (ASCIIEncoding.ASCII.GetBytes (
                        string.Format ("{0}:api_token", authManager.Token))));
            }
            return req;
        }

        private void PrepareResponse (HttpResponseMessage resp)
        {
            ServiceContainer.Resolve<MessageBus> ().Send (new TogglHttpResponseMessage (this, resp));
            resp.EnsureSuccessStatusCode ();
        }

        private async Task CreateModel<T> (Uri url, T model)
            where T : Model, new()
        {
            var json = ModelToJson (model);
            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Post,
                RequestUri = url,
                Content = new StringContent (json, Encoding.UTF8, "application/json"),
            });
            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);
            model.Merge (wrap.Data);
            // In case the local model has changed in the mean time (and merge does nothing),
            // make sure that the remote id is set.
            if (model.RemoteId == null)
                model.RemoteId = wrap.Data.RemoteId;
        }

        private async Task<T> GetModel<T> (Uri url, Func<T, T> selector = null)
            where T : Model, new()
        {
            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            selector = selector ?? Model.Update;

            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);

            return selector (wrap.Data);
        }

        private async Task UpdateModel<T> (Uri url, T model, Func<T, T> selector = null)
            where T : Model, new()
        {
            var json = ModelToJson (model);
            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Put,
                RequestUri = url,
                Content = new StringContent (json, Encoding.UTF8, "application/json"),
            });
            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);
            if (selector != null)
                wrap.Data = selector (wrap.Data);
            model.Merge (wrap.Data);
        }

        private async Task<List<T>> ListModels<T> (Uri url, Func<T, T> selector = null)
            where T : Model, new()
        {
            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = url,
            });

            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var models = JsonConvert.DeserializeObject<List<T>> (respData) ?? Enumerable.Empty<T> ();

            return models.Select (selector ?? Model.Update).ToList ();
        }

        private async Task DeleteModel (Uri url)
        {
            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Delete,
                RequestUri = url,
            });
            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);
        }

        private Task DeleteModels (Uri url)
        {
            return DeleteModel (url);
        }

        #region Client methods

        public Task CreateClient (ClientModel model)
        {
            var url = new Uri (v8Url, "clients");
            return CreateModel (url, model);
        }

        public Task<ClientModel> GetClient (long id)
        {
            var url = new Uri (v8Url, String.Format ("clients/{0}", id.ToString ()));
            return GetModel<ClientModel> (url);
        }

        public Task<List<ClientModel>> ListClients ()
        {
            var url = new Uri (v8Url, "clients");
            return ListModels<ClientModel> (url);
        }

        public Task<List<ClientModel>> ListWorkspaceClients (long workspaceId)
        {
            var url = new Uri (v8Url, String.Format ("workspaces/{0}/clients", workspaceId.ToString ()));
            return ListModels<ClientModel> (url);
        }

        public Task UpdateClient (ClientModel model)
        {
            var url = new Uri (v8Url, String.Format ("clients/{0}", model.RemoteId.Value.ToString ()));
            return UpdateModel (url, model);
        }

        public Task DeleteClient (ClientModel model)
        {
            var url = new Uri (v8Url, String.Format ("clients/{0}", model.RemoteId.Value.ToString ()));
            return DeleteModel (url);
        }

        #endregion

        #region Project methods

        public Task CreateProject (ProjectModel model)
        {
            var url = new Uri (v8Url, "projects");
            return CreateModel (url, model);
        }

        public Task<ProjectModel> GetProject (long id)
        {
            var url = new Uri (v8Url, String.Format ("projects/{0}", id.ToString ()));
            return GetModel<ProjectModel> (url);
        }

        public Task<List<ProjectModel>> ListWorkspaceProjects (long workspaceId)
        {
            var url = new Uri (v8Url, String.Format ("workspaces/{0}/projects", workspaceId.ToString ()));
            return ListModels<ProjectModel> (url);
        }

        public Task UpdateProject (ProjectModel model)
        {
            var url = new Uri (v8Url, String.Format ("projects/{0}", model.RemoteId.Value.ToString ()));
            return UpdateModel (url, model);
        }

        public Task DeleteProject (ProjectModel model)
        {
            var url = new Uri (v8Url, String.Format ("projects/{0}", model.RemoteId.Value.ToString ()));
            return DeleteModel (url);
        }

        public Task DeleteProjects (IEnumerable<ProjectModel> models)
        {
            var url = new Uri (v8Url, String.Format ("projects/{0}",
                          String.Join (",", models.Select ((model) => model.Id.Value.ToString ()))));
            return DeleteModels (url);
        }

        #endregion

        #region Task methods

        public Task CreateTask (TaskModel model)
        {
            var url = new Uri (v8Url, "tasks");
            return CreateModel (url, model);
        }

        public Task<TaskModel> GetTask (long id)
        {
            var url = new Uri (v8Url, String.Format ("tasks/{0}", id.ToString ()));
            return GetModel<TaskModel> (url);
        }

        public Task<List<TaskModel>> ListProjectTasks (long projectId)
        {
            var url = new Uri (v8Url, String.Format ("projects/{0}/tasks", projectId.ToString ()));
            return ListModels<TaskModel> (url);
        }

        public Task<List<TaskModel>> ListWorkspaceTasks (long workspaceId)
        {
            var url = new Uri (v8Url, String.Format ("workspaces/{0}/tasks", workspaceId.ToString ()));
            return ListModels<TaskModel> (url);
        }

        public Task UpdateTask (TaskModel model)
        {
            var url = new Uri (v8Url, String.Format ("tasks/{0}", model.RemoteId.Value.ToString ()));
            return UpdateModel (url, model);
        }

        public Task DeleteTask (TaskModel model)
        {
            var url = new Uri (v8Url, String.Format ("tasks/{0}", model.RemoteId.Value.ToString ()));
            return DeleteModel (url);
        }

        public Task DeleteTasks (IEnumerable<TaskModel> models)
        {
            var url = new Uri (v8Url, String.Format ("tasks/{0}",
                          String.Join (",", models.Select ((model) => model.Id.Value.ToString ()))));
            return DeleteModels (url);
        }

        #endregion

        #region Time entry methods

        public Task CreateTimeEntry (TimeEntryModel model)
        {
            var url = new Uri (v8Url, "time_entries");
            model.CreatedWith = Platform.DefaultCreatedWith;
            return CreateModel (url, model);
        }

        public Task<TimeEntryModel> GetTimeEntry (long id)
        {
            var url = new Uri (v8Url, String.Format ("time_entries/{0}", id.ToString ()));
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            return GetModel<TimeEntryModel> (url, (te) => {
                te.User = user;
                return Model.Update (te);
            });
        }

        public Task<List<TimeEntryModel>> ListTimeEntries ()
        {
            var url = new Uri (v8Url, "time_entries");
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            return ListModels<TimeEntryModel> (url, (te) => {
                te.User = user;
                return Model.Update (te);
            });
        }

        public Task<List<TimeEntryModel>> ListTimeEntries (DateTime start, DateTime end)
        {
            var url = new Uri (v8Url,
                          String.Format ("time_entries?start_date={0}&end_date={1}",
                              WebUtility.UrlEncode (start.ToUtc ().ToString ("o")),
                              WebUtility.UrlEncode (end.ToUtc ().ToString ("o"))));
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            return ListModels<TimeEntryModel> (url, (te) => {
                te.User = user;
                return Model.Update (te);
            });
        }

        public Task UpdateTimeEntry (TimeEntryModel model)
        {
            var url = new Uri (v8Url, String.Format ("time_entries/{0}", model.RemoteId.Value.ToString ()));
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            return UpdateModel (url, model, (te) => {
                te.User = user;
                return te;
            });
        }

        public Task DeleteTimeEntry (TimeEntryModel model)
        {
            var url = new Uri (v8Url, String.Format ("time_entries/{0}", model.RemoteId.Value.ToString ()));
            return DeleteModel (url);
        }

        #endregion

        #region Workspace methods

        public Task CreateWorkspace (WorkspaceModel model)
        {
            var url = new Uri (v8Url, "workspaces");
            return CreateModel (url, model);
        }

        public Task<WorkspaceModel> GetWorkspace (long id)
        {
            var url = new Uri (v8Url, String.Format ("workspaces/{0}", id.ToString ()));
            return GetModel<WorkspaceModel> (url);
        }

        public Task<List<WorkspaceModel>> ListWorkspaces ()
        {
            var url = new Uri (v8Url, "workspaces");
            return ListModels<WorkspaceModel> (url);
        }

        public Task UpdateWorkspace (WorkspaceModel model)
        {
            var url = new Uri (v8Url, String.Format ("workspaces/{0}", model.RemoteId.Value.ToString ()));
            return UpdateModel (url, model);
        }

        #endregion

        #region User methods

        public Task CreateUser (UserModel model)
        {
            var url = new Uri (v8Url, "signups");
            model.CreatedWith = Platform.DefaultCreatedWith;
            return CreateModel (url, model);
        }

        public Task<UserModel> GetUser (long id)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.Token == null || authManager.User.RemoteId != (long?)id)
                throw new NotSupportedException ("Can only update currently logged in user.");

            var url = new Uri (v8Url, "me");
            return GetModel<UserModel> (url);
        }

        public async Task<UserModel> GetUser (string username, string password)
        {
            var url = new Uri (v8Url, "me");

            var httpReq = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = url,
            };
            httpReq.Headers.Authorization = new AuthenticationHeaderValue ("Basic",
                Convert.ToBase64String (ASCIIEncoding.ASCII.GetBytes (
                    string.Format ("{0}:{1}", username, password))));
            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var wrap = JsonConvert.DeserializeObject<Wrapper<UserModel>> (respData);

            return Model.Update (wrap.Data);
        }

        public Task UpdateUser (UserModel model)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.Token == null || authManager.UserId != model.Id)
                throw new NotSupportedException ("Can only update currently logged in user.");

            var url = new Uri (v8Url, "me");
            return UpdateModel (url, model);
        }

        #endregion

        public async Task<UserRelatedModels> GetChanges (DateTime? since)
        {
            since = since.ToUtc ();
            var relUrl = "me?with_related_data=true";
            if (since.HasValue)
                relUrl = String.Format ("{0}&since={1}", relUrl, (long)(since.Value - UnixStart).TotalSeconds);
            var url = new Uri (v8Url, relUrl);

            var httpReq = SetupRequest (new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            var httpResp = await httpClient.SendAsync (httpReq);
            PrepareResponse (httpResp);

            var respData = await httpResp.Content.ReadAsStringAsync ();
            var json = JObject.Parse (respData);

            var user = Model.Update (json ["data"].ToObject<UserModel> ());
            return new UserRelatedModels () {
                Timestamp = UnixStart + TimeSpan.FromSeconds ((long)json ["since"]),
                User = user,
                Workspaces = GetChangesModels<WorkspaceModel> (json ["data"] ["workspaces"]),
                Clients = GetChangesModels<ClientModel> (json ["data"] ["clients"]),
                Projects = GetChangesModels<ProjectModel> (json ["data"] ["projects"]),
                Tasks = GetChangesModels<TaskModel> (json ["data"] ["tasks"]),
                TimeEntries = GetChangesTimeEntryModels (json ["data"] ["time_entries"], user),
            };
        }

        private IEnumerable<T> GetChangesModels<T> (JToken json)
            where T : Model, new()
        {
            if (json == null)
                return Enumerable.Empty<T> ();
            return json.ToObject<List<T>> ().Select (Model.Update);
        }

        private IEnumerable<TimeEntryModel> GetChangesTimeEntryModels (JToken json, UserModel user)
        {
            if (json == null)
                return Enumerable.Empty<TimeEntryModel> ();
            return json.ToObject<List<TimeEntryModel>> ().Select ((te) => {
                te.User = user;
                return Model.Update (te);
            });
        }

        private class Wrapper<T>
        {
            [JsonProperty ("data")]
            public T Data { get; set; }

            [JsonProperty ("since", NullValueHandling = NullValueHandling.Ignore)]
            public long? Since { get; set; }
        }
    }
}
