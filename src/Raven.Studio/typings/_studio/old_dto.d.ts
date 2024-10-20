/// <reference path="../../typescript/common/constants.ts"/>

interface collectionInfoDto extends indexResultsDto<documentDto> {
}

interface documentDto extends metadataAwareDto {
    [key:string]: any;
}

interface conflictsInfoDto extends indexResultsDto<conflictDto> {
}

interface dictionary<TValue> {
    [key: string]: TValue;
}

interface metadataAwareDto {
    '@metadata'?: documentMetadataDto;
}

interface logNotificationDto {
    Level: string;
    TimeStamp: string;
    LoggerName: string;
    RequestId: number;
    HttpMethod: string;
    ElapsedMilliseconds: number;
    ResponseStatusCode: number;
    RequestUri: string;
    TenantName: string;
    CustomInfo: string;
    InnerRequestsCount?: number;
    QueryTimings: any;

}

interface documentMetadataDto {
    'Raven-Entity-Name'?: string;
    'Raven-Clr-Type'?: string;
    'Non-Authoritative-Information'?: boolean;
    '@id'?: string;
    'Temp-Index-Score'?: number;
    'Last-Modified'?: string;
    'Raven-Last-Modified'?: string;
    '@etag'?: number;
}

interface databaseStatisticsDto {
    ApproximateTaskCount: number;
    CountOfDocuments: number;
    CountOfIndexes: number;
    CurrentNumberOfItemsToIndexInSingleBatch: number;
    CountOfStaleIndexesExcludingDisabledAndAbandoned: number;
    CountOfIndexesExcludingDisabledAndAbandoned: number;
    CurrentNumberOfItemsToReduceInSingleBatch: number;
    DatabaseId: string;
    DatabaseTransactionVersionSizeInMB: number;
    Errors: serverErrorDto[];
    InMemoryIndexingQueueSizes: number[];
    Indexes: indexStatisticsDto[];
    LastDocEtag: string;
    LastIndexingDateTime: string;
    Prefetches: futureBatchStatsDto[];
    StaleIndexes: string[];
    SupportsDtc: boolean;
    Is64Bit: boolean;
}

interface futureBatchStatsDto {
    Timestamp: string;
    Duration: string;
    Size: number;
    Retries: number;
    PrefetchingUser: string;
}

interface indexStatisticsDto {
    Name: string;
    IndexingAttempts: number;
    IndexingSuccesses: number;
    IndexingErrors: number;
    LastIndexedEtag: string;
    LastIndexedTimestamp: string;
    LastQueryTimestamp: string;
    TouchCount: number;
    Priority: string;
    ReduceIndexingAttempts: number;
    ReduceIndexingSuccesses: number;
    ReduceIndexingErrors: number;
    LastReducedEtag: string;
    LastReducedTimestamp: string;
    CreatedTimestamp: string;
    LastIndexingTime: string;
    IsOnRam: string; // Yep, it's really a string. Example values: "false", "true (3 KBytes)"
    LockMode: string;
    IsMapReduce: boolean;
    ForEntityName: string[];
    Performance: indexPerformanceDto[];
    DocsCount: number;
    IsInvalidIndex: boolean;
    IsTestIndex: boolean;
}

interface indexingBatchInfoDto {
    Id: number;
    BatchType: string;
    IndexesToWorkOn: string[];
    TotalDocumentCount: number;
    TotalDocumentSize: number;
    StartedAt: string; // ISO date string.
    StartedAtDate?: Date;
    TotalDurationMs: number;
    PerfStats: indexNameAndMapPerformanceStats[];  
}

interface indexNameAndMapPerformanceStats {
    indexName: string;
    stats: indexPerformanceDto;
    CacheThreadCount?: number;
}

interface indexPerformanceDto {
    Operation: string;
    ItemsCount: number;
    InputCount: number;
    OutputCount: number;
    Started: string; // Date
    Completed: string; // Date
    Duration: string;
    DurationMilliseconds: number;
    Operations: basePerformanceStatsDto[];
    WaitingTimeSinceLastBatchCompleted: string;
}

interface reducingBatchInfoDto {
    Id: number;
    IndexesToWorkOn: string[];
    StartedAt: string; // ISO date string.
    StartedAtDate?: Date;
    TotalDurationMs: number;
    TimeSinceFirstReduceInBatchCompletedMs: number;
    PerfStats: indexNameAndReducingPerformanceStats[];
}

interface deletionBatchInfoDto {
    Id: number;
    IndexName: string;
    TotalDocumentCount: number;
    StartedAt: string; // ISO date string.
    StartedAtDate?: Date;
    TotalDurationMs: number;
    PerformanceStats: deletionPerformanceStatsDto[];
}

interface indexNameAndReducingPerformanceStats {
    indexName: string;
    stats: reducePerformanceStatsDto;
    parent?: reducingBatchInfoDto;
}

interface reducePerformanceStatsDto {
    ReduceType?: string;
    LevelStats: reduceLevelPeformanceStatsDto[];
}

interface deletionPerformanceStatsDto extends basePerformanceStatsDto {
    Name: string;
}

interface reduceLevelPeformanceStatsDto {
    Level: number;
    ItemsCount: number;
    InputCount: number;
    OutputCount: number;
    Started: string; // ISO date string
    Completed: string; // Date
    Duration: string;
    DurationMs: number;
    Operations: basePerformanceStatsDto[];
    parent?: indexNameAndReducingPerformanceStats;
    CacheThreadCount?: number;
}

interface basePerformanceStatsDto {
    DurationMs: number;
    CacheWidth?: number;
    CacheCumulativeSum?: number;
    CacheIsSingleThread?: boolean;
}

interface performanceStatsDto extends basePerformanceStatsDto {
    Name: string;
    ParallelParent?: parallelBatchStatsDto;
}

interface parallelPefromanceStatsDto extends basePerformanceStatsDto {
    NumberOfThreads: number;
    BatchedOperations: parallelBatchStatsDto[];
}

interface parallelBatchStatsDto {
    StartDelay: number;
    Operations: performanceStatsDto[];
    Parent?: parallelPefromanceStatsDto;
}

interface apiKeyDto extends documentDto {
    Name: string;
    Secret: string;
    Enabled: boolean;
    Databases: Array<databaseAccessDto>;
}

interface serverBuildVersionDto {
    BuildVersion: number;
    ProductVersion: string;
    CommitHash: string;
    FullVersion: string;
}

interface latestServerBuildVersionDto {
    LatestBuild: number;
    Exception: string;
}

interface clientBuildVersionDto {
    Version: string;
}

interface supportCoverageDto {
    Status: string;
    EndsAt: string;
}

interface HotSpareDto {
    ActivationMode: string;
    ActivationTime: string;
    RemainingTestActivations: number;
}

interface userInfoDto {
    Remark: string;
    User: string;
    IsAdminGlobal: boolean;
    IsAdminCurrentDb: boolean;
    Databases: string[];
    Principal: string;
    AdminDatabases: string[];
    ReadOnlyDatabases: string[];
    ReadWriteDatabases: string[];
    AccessTokenBody: string;
}

interface serverConfigsDto {
    IsGlobalAdmin: boolean;
    CanReadWriteSettings: boolean;
    CanReadSettings: boolean;
    CanExposeConfigOverTheWire: boolean;
}

interface logDto {
    TimeStamp: string;
    Message: string;
    Database: string;
    LoggerName: string;
    Level: string;
    Exception: string;
    StackTrace: string;
}

interface serverErrorDto {
    Index: number;
    IndexName: string;
    Error: string;
    Timestamp: string;
    Document: string;
    Action: string;
}

interface replicationStatsDocumentDto {
    Self: string; // e.g. "http://judah-pc:8080/databases/ReplSrc"
    MostRecentDocumentEtag: string;
    Stats: replicationStatsDto[];
}

interface replicationStatsDto {
    FailureCountInternal: number;
    Url: string;
    LastHeartbeatReceived: string;
    LastEtagCheckedForReplication: string;
    LastReplicatedEtag: string;
    LastReplicatedLastModified: string;
    LastSuccessTimestamp: string;
    LastFailureTimestamp: string;
    FailureCount: number;
    LastError: string;
}

interface documentCountDto {
    Count: number;
    Type: string;
    IsEtl: boolean;
}

/*
 * Represents a spatial field of an index. Shows up in the Edit Index view when the index has spatial fields defined.
*/
interface spatialIndexFieldDto {
    Type: string;
    Strategy: string;
    MaxTreeLevel: number;
    MinX: number;
    MaxX: number;
    MinY: number;
    MaxY: number;
    Units: string;
}

interface spatialIndexSuggestionDto {
    Distance: string;
    Accuracy: number;
}


interface indexResultsDto<T extends metadataAwareDto> {
    DurationMilliseconds: number;
    Highlightings: any;
    Includes: any;
    IndexEtag: string;
    IndexName: string;
    IndexTimestamp: string;
    IsStale: boolean;
    LastQueryTime: string;
    NonAuthoritativeInformation: boolean;
    ResultEtag: string;
    Results: T[];
    SkippedResults: number;
    TotalResults: number;
}

interface documentPreviewDto {
    Results: documentDto[];
    TotalResults: number;
}

interface indexQueryResultsDto extends indexResultsDto<documentDto> {
    Error?: string;
}


interface replicationDestinationDto {
    Url: string;
    Username: string;
    Password: string;
    Domain: string;
    ApiKey: string;
    Database: string;
    TransitiveReplicationBehavior: string;
    IgnoredClient: boolean;
    Disabled: boolean;
    ClientVisibleUrl: string;
    SkipIndexReplication: boolean;
    SpecifiedCollections: dictionary<string>;
    HasGlobal?: boolean;
    HasLocal?: boolean;
}

interface configurationDocumentDto<TClass> {
    LocalExists?: boolean;
    GlobalExists?: boolean;
    MergedDocument: TClass;
    GlobalDocument?: TClass;
    Etag?: string;
    Metadata?: any;
}

interface configurationSettingDto {
    LocalExists: boolean;
    GlobalExists: boolean;
    EffectiveValue: string;
    GlobalValue: string;
}

interface configurationSettingsDto {
    Results: dictionary<configurationSettingDto>;
}

interface replicationsDto {
    Destinations: replicationDestinationDto[];
    Source: string;
    ClientConfiguration?: replicationClientConfigurationDto;
}

interface replicationClientConfigurationDto {
    FailoverBehavior?: string;
    RequestTimeSlaThresholdInMilliseconds: number;
}

interface environmentColorDto {
    Name: string;
    BackgroundColor: string;
}

interface replicationConfigDto {
    DocumentConflictResolution: string;    
}

interface databaseAccessDto {
    Admin: boolean;
    TenantId: string;
    ReadOnly: boolean;
}

interface windowsAuthDataDto {
    Name: string;
    Enabled: boolean;
    Databases: databaseAccessDto[];
}

interface windowsAuthDto {
    RequiredGroups: windowsAuthDataDto[];
    RequiredUsers: windowsAuthDataDto[];
}

interface transformerParamDto {
    name: string;
    value: string;
}

interface transformerQueryDto {
    transformerName: string;
    queryParams: Array<transformerParamDto>;
}

interface storedQueryDto {
    IsPinned: boolean;
    IndexName: string;
    QueryText: string;
    Sorts: string[];
    TransformerQuery: transformerQueryDto;
    ShowFields: boolean;
    IndexEntries: boolean;
    UseAndOperator: boolean;
    Hash: number;
}

interface storedPatchDto extends patchDto {
    Hash: number;
}

interface indexDataDto {
    name: string;
    hasReduce: boolean;
}

interface bulkDocumentDto {
    Key: string;
    Method: string;
    AdditionalData?: any[];
    Document?: documentDto; // Can be null when Method == "DELETE"
    Metadata?: documentMetadataDto; // Can be null when Method == "DELETE"
    Etag?: number; // Often is null on sending to server, non-null when returning from server.
    PatchResult?: any;
    Deleted?: any;
    DebugMode?: boolean;
    Patch?: scriptedPatchRequestDto;
}

interface scriptedPatchRequestDto {
    Script: string;
    Values: any;
}

interface databaseDocumentSaveDto {
    Key: string;
    ETag: number;
}

interface backupRequestDto {
    BackupLocation: string;
    DatabaseDocument: databaseDocumentDto;
}

interface backupStatusDto {
    Started: string;
    Completed?: string;
    Success?: string;
    IsRunning: boolean;
    Messages: backupMessageDto[];
}

interface backupMessageDto {
    Message: string;
    Timestamp: string;
    Severity: string;
}

interface databaseDocumentDto {
    Id: string;
    Settings: {};
    SecuredSettings: {};
    Disabled: boolean;
}

interface restoreRequestDto {
    BackupLocation: string;
    IndexesLocation: string;
    JournalsLocation: string;
}

interface databaseRestoreRequestDto extends restoreRequestDto {
    DatabaseName: string;
    DatabaseLocation: string;
    DisableReplicationDestinations: boolean;
    GenerateNewDatabaseId?: boolean;
}

interface filesystemRestoreRequestDto extends restoreRequestDto {
    FilesystemName: string;
    FilesystemLocation: string;
}

interface restoreStatusDto {
    Messages: string[];
    State: string;
}

interface compactStatusDto {
    Messages: string[];
    LastProgressMessage: string;
    State: string;
}

interface commandData {
    CommandText: string;
    Params:{Key:string;Value:any}[];
}

interface tableQuerySummary {
    TableName: string;
    Commands: commandData[];
}

interface sqlReplicationSimulationResultDto {
    Results: tableQuerySummary[];
    //TODO: LastAlert: alertDto;
}

interface facetDto {
    Mode: number; // Default = 0, Ranges = 1
    Aggregation: number; // None = 0, Count = 1, Max = 2, Min = 4, Average = 8, Sum = 16
    AggregationField: string;
    AggregationType: string;
    Name: string;
    DisplayName: string;
    Ranges: any[];
    MaxResults: number;
    TermSortMode: number;
    IncludeRemainingTerms: boolean;
}

interface facetResultSetDto {
    Results: any; // An object containing keys that look like [FacetName]-[FacetAggregationField]. For example "Company-Total". Each key will be of type facetResultDto.
    Duration: string;
}

interface facetResultDto {
    Values: facetValueDto[];
    RemainingTerms: string[];
    RemainingTermsCount: number;
    RemainingHits: number;
}

interface facetValueDto {
    Range: string;
    Hits: number;
    Count: number;
    Sum: number;
    Max: number;
    Min: number;
    Average: number;
}

interface scriptedIndexDto extends documentDto {
    IndexScript: string;
    DeleteScript: string;
}

interface conflictDto extends documentDto {
    Id: string;
    ConflictDetectedAt: string;
    Versions: conflictVersionsDto[];
}

interface replicationSourceDto extends documentDto {
    LastDocumentEtag?: string;
    ServerInstanceId: string;
    Source: string;
}

interface conflictVersionsDto {
    Id: string;
    SourceId: string;
}

interface documentBase extends dictionary<any> {
    getId(): string;
    getUrl(): string;
    getDocumentPropertyNames(): Array<string>;
}

interface ICollectionBase {
    colorClass: string;
}

interface customColumnParamsDto {
    Header?: string;
    Binding: string;
    DefaultWidth?: number;
    Template?: string;
}

interface customColumnsDto {
    Columns: Array<customColumnParamsDto>;
}

interface patchValueDto {
    Key: string;
    Value: string;
}

interface patchDto extends documentDto {
    PatchOnOption: string;
    Query: string;
    Script: string;
    SelectedItem: string;
    Values: Array<patchValueDto>;
}

interface statusStorageOnDiskDto {
    TransactionalStorageAllocatedSize: number;
    TransactionalStorageAllocatedSizeHumaneSize: string;
    TransactionalStorageUsedSize: number;
    TransactionalStorageUsedSizeHumaneSize: string;
    IndexStorageSize: number;
    IndexStorageSizeHumane: string;
    TotalDatabaseSize: number;
    TotalDatabaseSizeHumane: string;
}

interface statusDebugChangesDto {
    Id: string;
    Connected: boolean;
    DocumentStore: statusDebugChangesDocumentStoreDto;
    FileSystem: statusDebugChangesFileSystemDto;
    CounterStorage: statusDebugChangesCounterStorageDto;
    TimeSeries: statusDebugChangesTimeSeriesDto;
    WatchAllDocuments: boolean;
    WatchAllIndexes: boolean;
    WatchConfig: boolean;
    WatchConflicts: boolean;
    WatchSync: boolean;
    WatchCancellations: boolean;
    WatchDocumentPrefixes: Array<string>;
    WatchDocumentsInCollection: Array<string>;
    WatchIndexes: Array<string>;
    WatchDocuments: Array<string>;
    WatchedFolders: Array<string>;
}

interface statusDebugDataSubscriptionsDto {
    SubscriptionId: number;
    Criteria: subscriptionCriteriaDto;
    AckEtag: string;
    TimeOfSendingLastBatch: string;
    TimeOfLastClientActivity: string;
    IsOpen: boolean;
    ConnectionOptions: subscriptionConnectionOptionsDto;
}

interface subscriptionDto {
    SubscriptionId: number;
    AckEtag: string;
}

interface subscriptionCriteriaDto {
    KeyStartsWith: string;
    BelongsToAnyCollection: Array<string>;
    PropertiesMatch: Array<{ Key: string; Value: string}>;
    PropertiesNotMatch: Array<{ Key: string; Value: string }>;
}

interface subscriptionConnectionOptionsDto {
    ConnectionId: string;
    BatchOptions: subscriptionBatchOptionsDto;
    ClientAliveNotificationInterval: string;
    IgnoreSubscribersErrors: boolean;
}

interface subscriptionBatchOptionsDto {
    MaxSize: number;
    MaxDocCount: number;
    AcknowledgmentTimeout: string;
}

interface statusDebugChangesDocumentStoreDto {
    WatchAllDocuments: boolean;
    WatchAllIndexes: boolean;
    WatchAllTransformers: boolean;
    WatchAllReplicationConflicts: boolean;
    WatchedIndexes: Array<string>;
    WatchedDocuments: Array<string>;
    WatchedDocumentPrefixes: Array<string>;
    WatchedDocumentsInCollection: Array<string>;
    WatchedDocumentsOfType: Array<string>;
    WatchedBulkInserts: Array<string>;
}

interface statusDebugChangesFileSystemDto {
    WatchConflicts: boolean;
    WatchSync: boolean;
    WatchCancellations: boolean;
    WatchConfig: boolean;
    WatchedFolders: Array<string>;
}

interface statusDebugMetricsDto {
    DocsWritesPerSecond: number;
    IndexedPerSecond: number;
    ReducedPerSecond: number;
    RequestsPerSecond: number;
    Requests: meterDataDto;
    RequestsDuration: histogramDataDto;
    StaleIndexMaps: histogramDataDto;
    StaleIndexReduces: histogramDataDto;
    Gauges: any;
    ReplicationBatchSizeMeter: dictionary<meterDataDto>;
    ReplicationDurationMeter: dictionary<meterDataDto>;
    ReplicationBatchSizeHistogram: dictionary<histogramDataDto>;
    ReplicationDurationHistogram: dictionary<histogramDataDto>;
}

interface meterDataDto {
    Count: number;
    MeanRate: number;
    OneMinuteRate: number;
    FiveMinuteRate: number;
    FifteenMinuteRate: number;
}

interface histogramDataDto {
    Counter: number;
    Max: number;
    Min: number;
    Mean: number;
    Stdev: number;
    Percentiles: any;
}

interface statusDebugDocrefsDto {
    TotalCount: number;
    Results: Array<string>;
}

interface statusDebugIdentitiesDto {
    TotalCount: number;
    Identities: Array<{ Key: string; Value: string}>;
}

interface statusDebugCurrentlyIndexingDto {
    NumberOfCurrentlyWorkingIndexes: number;
    Indexes: Array<statusDebugIndexDto>;
}

interface statusDebugIndexDto {
    IndexName: string;
    IsMapReduce: boolean;
    RemainingReductions: number;
    CurrentOperations: Array<statusDebugIndexOperationDto>;
    Priority: string;
    OverallIndexingRate: Array<statusDebugIndexRateDto>;
}

interface statusDebugIndexOperationDto {
    Operation: string;
    NumberOfProcessingItems: number;
}

interface statusDebugIndexRateDto {
    Operation: string;
    Rate: string;
}

interface statusDebugQueriesGroupDto {
    IndexName: string;
    Queries: Array<statusDebugQueriesQueryDto>;
}

interface statusDebugQueriesQueryDto {
    StartTime: string;
    QueryInfo: string;
    QueryId: number;
    Duration: string;
}

interface taskMetadataDto {
    Id: any;
    IndexId: number;
    IndexName: string;
    AddedTime: string;
    Type: string;
}

interface taskMetadataSummaryDto {
    Type: string;
    IndexId: number;
    IndexName: string;
    Count: number;
    MinDate: string;
    MaxDate: string;
}

interface requestTracingDto {
    Uri: string;
    Method: string;
    StatusCode: number;
    RequestHeaders: requestHeaderDto[];
    ExecutionTime: string;
    AdditionalInfo: string;
}

interface requestHeaderDto {
    Name: string;
    Values: string[];
}

interface sqlReplicationStatsDto {
    Name: string;
    Statistics: any;
    Metrics: sqlReplicaitonMetricsDto;
}
interface sqlReplicaitonMetricsDto {
    GeneralMetrics: dictionary<metricsDataDto>;
    TablesMetrics: dictionary<dictionary<metricsDataDto>>;
}
interface metricsDataDto {
    Type: string;
    Name:string;
}

interface statusDebugIndexFieldsDto {
    FieldNames: string[];
}

interface debugDocumentStatsDto {
    Total: number;
    TotalSize: number;
    Tombstones: number;
    System: collectionStats;
    NoCollection: collectionStats;
    Collections: dictionary<collectionStats>;
    TimeToGenerate: string;
}

interface collectionStats {
    Stats: histogramDataDto;
    TotalSize: number;
    TopDocs: any[];
}

interface resourceStyleMap {
    resourceName: string;
    styleMap: any;
}

interface timeSeriesDto {
    Name: string;
    Path?: string;
}

interface changesApiEventDto {
    Time: string; // ISO date string
    Type: string;
    Value?: any;
}

interface databaseDto extends tenantDto {
    IndexingDisabled: boolean;
    RejectClientsEnabled: boolean;
    ClusterWide: boolean;
}

interface tenantDto {
    IsLoaded: boolean;
    Name: string;
    Disabled: boolean;
    Bundles: Array<string>;
    IsAdminCurrentTenant: boolean;
}

interface fileSystemDto extends tenantDto {
}

interface counterStorageDto extends tenantDto {
}

interface timeSeriesDto extends tenantDto {
}

interface customFunctionsDto {
    Functions: string;
}

interface suggestionsDto {
    Suggestions: Array<string>;
}

interface queryFieldInfo {
    Index: number;
    FieldName: string;
    FieldValue: string;
}

interface indexSuggestion extends queryFieldInfo {
    Suggestion: string;
}

interface mappedResultInfo {
    ReduceKey?: string;
    Timestamp?: string;
    Etag?: string;
    Data?: any;
    Bucket?: number;
    Source?: string;
}


interface visualizerDataObjectDto {
    x?: number;
    y?: number;
    type: number;
    id: any;
    source?: any;
    idx: number;
}

interface visualizerDataObjectNodeDto {
    children?: visualizerDataObjectNodeDto[];
    name?: string;
    level?: number;
    origin?: visualizerDataObjectNodeDto;
    x?: number;
    y?: number;
    depth?: number;
    parent?: visualizerDataObjectNodeDto;
    payload?: mappedResultInfo;
    connections?: visualizerDataObjectNodeDto[];
    cachedId?: string;
}

interface queryIndexDebugMapArgsDto {
    key?: string;
    sourceId?: string;
    startsWith?: string;
}

interface graphLinkDto {
    source: visualizerDataObjectNodeDto;
    target: visualizerDataObjectNodeDto;
    cachedId?: string;
}

interface mergeResult {
  Document: string;
  Metadata: string;
}

interface visualizerExportDto {
    indexName: string;
    docKeys: string[];
    reduceKeys: string[];
    tree: visualizerDataObjectNodeDto;
}

interface operationStatusDto {
    Completed: boolean;
    Faulted: boolean;
    Canceled: boolean;
    State: operationStateDto;
}

interface operationStateDto {
    Error?: string;
    Progress?: string;
}

interface bulkOperationStatusDto extends operationStatusDto {
    OperationProgress: bulkOperationProgress;
}

interface internalStorageBreakdownState extends operationStatusDto {
    ReportResults: string[];
}

interface debugDocumentStatsStateDto extends operationStatusDto {
    Stats: debugDocumentStatsDto;
}

interface documentStateDto {
    Document: string;
    Deleted: boolean;
}

interface bulkOperationProgress {
    TotalEntries: number;
    ProcessedEntries: number;
}


interface dataDumperOperationStatusDto extends operationStatusDto {
    ExceptionDetails: string;
}

interface importOperationStatusDto extends operationStatusDto{
    LastProgress: string;
    ExceptionDetails: string;
}

interface globalTopologyDto {
    Databases: replicationTopologyDto;
    FileSystems: synchronizationTopologyDto;
    Counters: countersReplicationTopologyDto;
}

interface replicationTopologyDto {
    Servers: string[];
    Connections: replicationTopologyConnectionDto[];
    SkippedResources: string[];
}

interface synchronizationTopologyDto {
    Servers: string[];
    Connections: synchronizationTopologyConnectionDto[];
    SkippedResources: string[];
}

interface countersReplicationTopologyDto {
    Servers: string[];
    Connections: countersReplicationTopologyConnectionDto[];
    SkippedResources: string[];
}

interface replicationTopologyConnectionDto {
    Destination: string;
    DestinationToSourceState: string;
    Errors: string[];
    LastDocumentEtag: string;
    ReplicationBehavior: string;
    SendServerId: string;
    Source: string;
    SourceToDestinationState: string;
    StoredServerId: string;
    UiType: string;
}

interface synchronizationTopologyConnectionDto {
    Destination: string;
    DestinationToSourceState: string;
    Errors: string[];
    LastSourceFileEtag: string;
    SendServerId: string;
    Source: string;
    SourceToDestinationState: string;
    StoredServerId: string;
    UiType: string;
}

interface countersReplicationTopologyConnectionDto {
    Destination: string;
    DestinationToSourceState: string;
    Errors: string[];
    LastEtag: string;
    SendServerId: string;
    Source: string;
    SourceToDestinationState: string;
    StoredServerId: string;
    UiType: string;
}

interface runningTaskDto {
    Id: number;
    Status: operationStateDto;
    Exception: string;
    Killable: boolean;
    Completed: boolean;
    Faulted: boolean;
    Canceled: boolean;
    Description: string;
    TaskType: string;
    StartTime: string;
}


interface adminLogsConfigEntryDto {
    category: string;
    level: string;
    includeStackTrace: boolean;
}

interface fileSystemSettingsDto {
    name: string;
    path: string;
    logsPath: string;
    storageEngine: string;
}

interface performanceTestRequestDto {
    Path: string;
    FileSize: number;
    TestType: string;

    OperationType?: string;
    BufferingType?: string;
    Sequential?: boolean;
    ThreadCount?: number;
    TimeToRunInSeconds?: number;
    RandomSeed?: number;
    ChunkSize?: number;

    NumberOfDocuments?: number;
    SizeOfDocuments?: number;
    NumberOfDocumentsInBatch?: number;
    WaitBetweenBatches?: number;
}

interface diskPerformanceResultDto {
    ReadPerSecondHistory: number[];
    WritePerSecondHistory: number[];
    AverageReadLatencyPerSecondHistory: number[];
    AverageWriteLatencyPerSecondHistory: number[];
    ReadLatency: histogramDataDto;
    WriteLatency: histogramDataDto;
    TotalRead: number;
    TotalWrite: number;
    TotalTimeMs: number;
}

interface diskPerformanceResultWrappedDto {
    Result: diskPerformanceResultDto;
    Request: performanceTestRequestDto;
    DebugMsgs: string[];
}

interface indexReplaceDocumentDto extends documentDto {
    IndexToReplace: string;
    MinimumEtagBeforeReplace?: string;
    ReplaceTimeUtc?: string;
}

interface replicationExplanationForDocumentDto {
    Key: string;
    Etag: string;
    Destination: destinationInformationDto;
    Message: string;
}

interface destinationInformationDto {
    Url: string;
    DatabaseName: string;
    ServerInstanceId: string;
    LastDocumentEtag: string;
}



interface synchronizationConfigDto {
    FileConflictResolution: string;
    MaxNumberOfSynchronizationsPerDestination: number;
    SynchronizationLockTimeoutMiliseconds: number;
}

interface pluginsInfoDto {
    Extensions: Array<extensionsLogDto>;
    Triggers: Array<triggerInfoDto>;
    CustomBundles: Array<string>;
}

interface extensionsLogDto {
    Name: string;
    Installed: Array<extensionsLogDetailDto>;
}

interface extensionsLogDetailDto {
    Name: string;
    Assembly: string;
}

interface triggerInfoDto {
    Type: string;
    Name: string;
}

interface copyFromParentDto<T> {
    copyFromParent(parent: T): void;
}
interface topologyDto {
    CurrentLeader: string;
    CurrentTerm: number;
    State: string;
    CommitIndex: number;
    AllVotingNodes: Array<nodeConnectionInfoDto>;
    PromotableNodes: Array<nodeConnectionInfoDto>;
    NonVotingNodes: Array<nodeConnectionInfoDto>;
    TopologyId: string;
}

interface nodeConnectionInfoDto {
    Uri: string;
    Name: string;
    Username?: string;
    Password?: string;
    Domain?: string;
    ApiKey?: string;
    IsNoneVoter?: boolean;
}

interface clusterConfigurationDto {
    EnableReplication: boolean;
    DatabaseSettings?: dictionary<string>;
}

interface clusterNodeStatusDto {
    Uri: string;
    Status: string;
}

interface serverSmugglingItemDto {
    Name: string;
    Incremental: boolean;
    StripReplicationInformation: boolean;
    ShouldDisableVersioningBundle: boolean;
}

interface serverConnectionInfoDto {
    Url: string;
    Username: string;
    Password: string;
    Domain: string;
    ApiKey: string;
}

interface serverSmugglingDto {
    TargetServer: serverConnectionInfoDto;
    Config: Array<serverSmugglingItemDto>;
}

interface serverSmugglingOperationStateDto extends operationStatusDto {
    Messages: Array<string>;
}

interface dataExplorationRequestDto {
    Linq: string;
    Collection: string;
    TimeoutSeconds: number;
    PageSize: number;
}

interface adminJsScriptDto {
    Script: string;
}


interface consoleJsSampleDto {
    Name: string;
    Code: string;
}



interface diskIoPerformanceRunDto {
    ProcessId: number;
    ProcessName: string;
    DurationInMinutes: number;
    StartTime: string;
    Databases: Array<diskIoPerformanceRunResultDto>;
}

interface diskIoPerformanceRunResultDto
{
    Name: string;
    Results: dictionary<Array<diskIoPerformanceRunIoResultDto>>;
}

interface diskIoPerformanceRunIoResultDto extends documentDto {
    PathType: string;
    WriteDurationInMilliseconds: number;
    WriteIoSizeInBytes: number;
    ReadDurationInMilliseconds: number;
    ReadIoSizeInBytes: number;
    NumberOfReadOperations: number;
    NumberOfWriteOperations: number;
}

interface performanceRunItemDto {
    displayName: string;
    documentId: string;
}


interface filteredOutIndexStatDto {
    Timestamp: string;
    TimestampParsed?: Date;
    IndexName: string;
}

interface generatedCodeDto {
    Document: string;
    Code: string;
}


interface fetcherDto<T> {
    (skip: number, take: number): JQueryPromise<PagedResultSet<T>>;
}

interface PagedResultSet<T> {
    items: Array<T>;
    totalResultCount: number;
    additionalResultInfo?: any;
}
