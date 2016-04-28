﻿using System;
using System.IO;
using NUnit.Framework;
using SQLite.Net;
using SQLite.Net.Platform.Generic;
using Toggl.Phoebe.Data;
using XPlatUtils;
using V0 = Toggl.Phoebe.Data.Models.Old.DB_VERSION_0;
using V1 = Toggl.Phoebe.Data.Models;

namespace Toggl.Phoebe.Tests.Data.Migration
{
    [TestFixture]
    public class MigrateV0toV1Test : Test
    {
        #region Setup

        [SetUp]
        public void SetUp()
        {
            this.setupV0database();
        }

        private void setupV0database()
        {
            var path = DatabaseHelper.GetDatabasePath(this.databaseDir, 0);
            if (File.Exists(path)) { File.Delete(path); }

            using (var db = new SQLiteConnection(new SQLitePlatformGeneric(), path))
            {
                db.CreateTable<V0.ClientData>();
                db.CreateTable<V0.ProjectData>();
                db.CreateTable<V0.ProjectUserData>();
                db.CreateTable<V0.TagData>();
                db.CreateTable<V0.TaskData>();
                db.CreateTable<V0.TimeEntryData>();
                db.CreateTable<V0.TimeEntryTagData>();
                db.CreateTable<V0.UserData>();
                db.CreateTable<V0.WorkspaceData>();
                db.CreateTable<V0.WorkspaceUserData>();
            }
        }

        #endregion

        #region Tests

        [Test]
        public void TestMigrateEmpty()
        {
            var store = this.migrate();

            Assert.AreEqual(1, store.GetVersion());
        }

        [Test]
        public void TestMigrateWorkspaceData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceName = "the matrix";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, Name = workspaceName }
            );

            var store = this.migrate();

            var workspace = store.Table<V1.WorkspaceData>().First();

            Assert.AreEqual(workspaceID, workspace.Id);
            Assert.AreEqual(workspaceName, workspace.Name);
        }

        [Test]
        public void TestMigrateUserData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var userRemoteID = 1337L;
            var userName = "neo";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.UserData { RemoteId = userRemoteID, Name = userName, DefaultWorkspaceId = workspaceID }
            );

            var store = this.migrate();

            var user = store.Table<V1.UserData>().First();

            Assert.AreEqual(userRemoteID, user.RemoteId);
            Assert.AreEqual(userName, user.Name);
            Assert.AreEqual(workspaceID, user.DefaultWorkspaceId);
            Assert.AreEqual(workspaceRemoteID, user.DefaultWorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateWorkspaceUserData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var userRemoteID = 1337L;
            var userID = Guid.NewGuid();

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.UserData { Id = userID, RemoteId = userRemoteID },
                new V0.WorkspaceUserData { UserId = userID, WorkspaceId = workspaceID }
            );

            var store = this.migrate();

            var wsUserData = store.Table<V1.WorkspaceUserData>().First();

            Assert.AreEqual(userID, wsUserData.UserId);
            Assert.AreEqual(userRemoteID, wsUserData.UserRemoteId);
            Assert.AreEqual(workspaceID, wsUserData.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, wsUserData.WorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateClientData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var clientRemoteId = 1337L;
            var clientName = "the oracle";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.ClientData { RemoteId = clientRemoteId, Name = clientName, WorkspaceId = workspaceID }
            );

            var store = this.migrate();

            var client = store.Table<V1.ClientData>().First();

            Assert.AreEqual(clientRemoteId, client.RemoteId);
            Assert.AreEqual(clientName, client.Name);
            Assert.AreEqual(workspaceID, client.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, client.WorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateProjectData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var clientId = Guid.NewGuid();
            var clientRemoteId = 1337L;
            var projectRemoteId = 500L;
            var projectName = "save the world";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.ClientData { Id = clientId, RemoteId = clientRemoteId, WorkspaceId = workspaceID },
                new V0.ProjectData { RemoteId = projectRemoteId, Name = projectName, ClientId = clientId, WorkspaceId = workspaceID }
            );

            var store = this.migrate();

            var project = store.Table<V1.ProjectData>().First();

            Assert.AreEqual(projectRemoteId, project.RemoteId);
            Assert.AreEqual(projectName, project.Name);
            Assert.AreEqual(clientId, project.ClientId);
            Assert.AreEqual(clientRemoteId, project.ClientRemoteId);
            Assert.AreEqual(workspaceID, project.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, project.WorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateProjectUserData()
        {
            var projectId = Guid.NewGuid();
            var projectRemoteId = 500L;
            var projectName = "save the world";
            var userRemoteID = 1337L;
            var userID = Guid.NewGuid();

            this.insertIntoV0Database(
                new V0.ProjectData { Id = projectId, RemoteId = projectRemoteId, Name = projectName },
                new V0.UserData { Id = userID, RemoteId = userRemoteID },
                new V0.ProjectUserData { UserId = userID, ProjectId = projectId }
            );

            var store = this.migrate();

            var projectUserData = store.Table<V1.ProjectUserData>().First();

            Assert.AreEqual(projectId, projectUserData.ProjectId);
            Assert.AreEqual(projectRemoteId, projectUserData.ProjectRemoteId);
            Assert.AreEqual(userID, projectUserData.UserId);
            Assert.AreEqual(userRemoteID, projectUserData.UserRemoteId);
        }

        [Test]
        public void TestMigrateTagData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var tagRemoteId = 500L;
            var tagName = "epic";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.TagData { RemoteId = tagRemoteId, Name = tagName, WorkspaceId = workspaceID }
            );

            var store = this.migrate();

            var tag = store.Table<V1.TagData>().First();

            Assert.AreEqual(tagRemoteId, tag.RemoteId);
            Assert.AreEqual(tagName, tag.Name);
            Assert.AreEqual(workspaceID, tag.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, tag.WorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateTaskData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var projectID = Guid.NewGuid();
            var projectRemoteId = 500L;
            var taskRemoteId = 1337L;
            var taskName = "become the one";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.ProjectData { Id = projectID, RemoteId = projectRemoteId, WorkspaceId = workspaceID },
                new V0.TaskData { RemoteId = taskRemoteId, Name = taskName, WorkspaceId = workspaceID, ProjectId = projectID }
            );

            var store = this.migrate();

            var task = store.Table<V1.TaskData>().First();

            Assert.AreEqual(taskRemoteId, task.RemoteId);
            Assert.AreEqual(taskName, task.Name);
            Assert.AreEqual(projectID, task.ProjectId);
            Assert.AreEqual(projectRemoteId, task.ProjectRemoteId);
            Assert.AreEqual(workspaceID, task.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, task.WorkspaceRemoteId);
        }

        [Test]
        public void TestMigrateTimeEntryData()
        {
            var workspaceID = Guid.NewGuid();
            var workspaceRemoteID = 42L;
            var projectID = Guid.NewGuid();
            var projectRemoteId = 500L;
            var taskId = Guid.NewGuid();
            var taskRemoteId = 1337L;
            var tagId = Guid.NewGuid();
            var tagName = "epic";

            var timeEntryId = Guid.NewGuid();
            var timeEntryDescription = "learning kung fu";

            this.insertIntoV0Database(
                new V0.WorkspaceData { Id = workspaceID, RemoteId = workspaceRemoteID },
                new V0.ProjectData { Id = projectID, RemoteId = projectRemoteId, WorkspaceId = workspaceID },
                new V0.TaskData { Id = taskId, RemoteId = taskRemoteId, WorkspaceId = workspaceID, ProjectId = projectID },
                new V0.TagData { Id = tagId, Name = tagName, WorkspaceId = workspaceID },
                new V0.TimeEntryData
            {
                Id = timeEntryId,
                Description = timeEntryDescription,
                WorkspaceId = workspaceID,
                ProjectId = projectID,
                TaskId = taskId,
                State = V1.TimeEntryState.Finished
            },
            new V0.TimeEntryTagData { TimeEntryId = timeEntryId, TagId = tagId }
            );

            var store = this.migrate();

            var timeEntry = store.Table<V1.TimeEntryData>().First();

            Assert.AreEqual(timeEntryId, timeEntry.Id);
            Assert.AreEqual(timeEntryDescription, timeEntry.Description);
            Assert.AreEqual(1, timeEntry.Tags.Count);
            Assert.AreEqual(tagName, timeEntry.Tags[0]);
            Assert.AreEqual(taskId, timeEntry.TaskId);
            Assert.AreEqual(taskRemoteId, timeEntry.TaskRemoteId);
            Assert.AreEqual(projectID, timeEntry.ProjectId);
            Assert.AreEqual(projectRemoteId, timeEntry.ProjectRemoteId);
            Assert.AreEqual(workspaceID, timeEntry.WorkspaceId);
            Assert.AreEqual(workspaceRemoteID, timeEntry.WorkspaceRemoteId);
        }

        #endregion

        #region Helpers

        private ISyncDataStore migrate()
        {
            var platformInfo = new SQLitePlatformGeneric();
            Action<float> dummyReporter = x => { };

            var oldVersion = DatabaseHelper.CheckOldDb(this.databaseDir);
            if (oldVersion != -1)
            {
                var success = DatabaseHelper.Migrate(
                    platformInfo, this.databaseDir,
                    oldVersion, SyncSqliteDataStore.DB_VERSION,
                    dummyReporter);

                if (!success)
                    throw new MigrationException("Migration unsuccessful");
            }

            return ServiceContainer.Resolve<ISyncDataStore>();
        }

        private void insertIntoV0Database(params object[] objects)
        {
            var dbPath = DatabaseHelper.GetDatabasePath(this.databaseDir, 0);
            using (var db = new SQLiteConnection(new SQLitePlatformGeneric(), dbPath))
            {
                db.InsertAll(objects);
            }
        }

        #endregion

    }
}

