﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using Dicom;
using Microsoft.Health.Api.Features.Audit;
using Microsoft.Health.Dicom.Client;
using Microsoft.Health.Dicom.Core.Extensions;
using Microsoft.Health.Dicom.Core.Features.Audit;
using Microsoft.Health.Dicom.Core.Features.Model;
using Microsoft.Health.Dicom.Tests.Common;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Dicom.Web.Tests.E2E.Rest.Audit
{
    /// <summary>
    /// Provides Audit specific tests.
    /// </summary
    public class AuditTests : IClassFixture<AuditTestFixture>
    {
        private readonly AuditTestFixture _fixture;
        private readonly IDicomWebClient _client;

        private readonly TraceAuditLogger _auditLogger;

        public AuditTests(AuditTestFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.Client;

            _auditLogger = _fixture.AuditLogger;
        }

        [Fact]
        public async Task GivenRetrieveRequest_WhenResourceIsFound_ThenAuditLogEntriesShouldBeCreated()
        {
            InstanceIdentifier dicomInstance = await CreateDicomFileAndGetInstanceIdentifierAsync();

            await ExecuteAndValidate(
                () => _client.RetrieveStudyAsync(dicomInstance.StudyInstanceUid),
                AuditEventSubType.Retrieve,
                $"studies/{dicomInstance.StudyInstanceUid}",
                HttpStatusCode.OK);
        }

        [Fact]
        public async Task GivenRetrieveMetadataRequest_WhenResourceIsFound_ThenAuditEntriesShouldBeCreated()
        {
            InstanceIdentifier dicomInstance = await CreateDicomFileAndGetInstanceIdentifierAsync();

            await ExecuteAndValidate(
                () => _client.RetrieveStudyMetadataAsync(dicomInstance.StudyInstanceUid),
                AuditEventSubType.RetrieveMetadata,
                $"studies/{dicomInstance.StudyInstanceUid}/metadata",
                HttpStatusCode.OK);
        }

        [Fact]
        public async Task GivenSearchRequest_WithValidParamsAndNoMatchingResult_ThenAuditLogEntriesShouldBeCreated()
        {
            await ExecuteAndValidate(
                () => _client.QueryAsync("/studies?StudyDate=20200101"),
                AuditEventSubType.Query,
                "studies?StudyDate=20200101",
                HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task GivenChangeFeedRequest_WhenAnInstanceExists_ThenAuditLogEntriesShouldBeCreated()
        {
            InstanceIdentifier dicomInstance = await CreateDicomFileAndGetInstanceIdentifierAsync();

            await ExecuteAndValidate(
                () => _client.GetChangeFeed(),
                AuditEventSubType.ChangeFeed,
                "changefeed",
                HttpStatusCode.OK);
        }

        [Fact]
        public async Task GivenDeleteRequest_WhenResourceExists_ThenAuditLogEntriesShouldBeCreated()
        {
            InstanceIdentifier dicomInstance = await CreateDicomFileAndGetInstanceIdentifierAsync();

            await ExecuteAndValidate(
                () => _client.DeleteStudyAsync(dicomInstance.StudyInstanceUid),
                AuditEventSubType.Delete,
                $"studies/{dicomInstance.StudyInstanceUid}",
                HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task GivenStoreRequest_WhenStoringUsingStudyInstanceUid_ThenAuditLogEntriesShouldBeCreated()
        {
            string studyInstanceUid = TestUidGenerator.Generate();
            DicomFile dicomFile = Samples.CreateRandomDicomFile(studyInstanceUid);
            InstanceIdentifier dicomInstance = dicomFile.Dataset.ToInstanceIdentifier();

            await ExecuteAndValidate(
                () => _client.StoreAsync(new[] { dicomFile }, studyInstanceUid),
                AuditEventSubType.Store,
                $"studies/{dicomInstance.StudyInstanceUid}",
                HttpStatusCode.OK);
        }

        private async Task ExecuteAndValidate<T>(Func<Task<T>> action, string expectedAction, string expectedPathSegment, HttpStatusCode expectedStatusCode)
        {
            if (!_fixture.IsUsingInProcTestServer)
            {
                // This test only works with the in-proc server with customized middleware pipeline
                return;
            }

            var response = await action();

            var expectedUri = new Uri($"http://localhost/{expectedPathSegment}");

            Assert.Collection(
                _auditLogger.GetAuditEntriesByOperationAndRequestUri(expectedAction, expectedUri),
                ae => ValidateExecutingAuditEntry(ae, expectedAction, expectedUri),
                ae => ValidateExecutedAuditEntry(ae, expectedAction, expectedUri, expectedStatusCode));
        }

        private void ValidateExecutingAuditEntry(AuditEntry auditEntry, string expectedAction, Uri expectedUri)
        {
            ValidateAuditEntry(auditEntry, AuditAction.Executing, expectedAction, expectedUri, null);
        }

        private void ValidateExecutedAuditEntry(AuditEntry auditEntry, string expectedAction, Uri expectedUri, HttpStatusCode? expectedStatusCode)
        {
            ValidateAuditEntry(auditEntry, AuditAction.Executed, expectedAction, expectedUri, expectedStatusCode);
        }

        private void ValidateAuditEntry(AuditEntry auditEntry, AuditAction expectedAuditAction, string expectedAction, Uri expectedUri, HttpStatusCode? expectedStatusCode)
        {
            Assert.NotNull(auditEntry);
            Assert.Equal(expectedAuditAction, auditEntry.AuditAction);
            Assert.Equal(expectedAction, auditEntry.Action);
            Assert.Equal(expectedUri, auditEntry.RequestUri);
            Assert.Equal(expectedStatusCode, auditEntry.StatusCode);
        }

        private async Task<InstanceIdentifier> CreateDicomFileAndGetInstanceIdentifierAsync()
        {
            string studyInstanceUid = TestUidGenerator.Generate();
            DicomFile dicomFile = Samples.CreateRandomDicomFile(studyInstanceUid);
            InstanceIdentifier dicomInstance = dicomFile.Dataset.ToInstanceIdentifier();
            await _client.StoreAsync(new[] { dicomFile }, studyInstanceUid);

            return dicomInstance;
        }
    }
}