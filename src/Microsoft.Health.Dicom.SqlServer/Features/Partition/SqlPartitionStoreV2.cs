﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------
using Microsoft.Health.Dicom.SqlServer.Features.Schema;

namespace Microsoft.Health.Dicom.SqlServer.Features.Partition
{
    internal class SqlPartitionStoreV2 : SqlPartitionStoreV1
    {
        public override SchemaVersion Version => SchemaVersion.V2;
    }
}
