# DC-Alpha-AzureSearch-POC

## Introduction

Provides multiple test projects for investigating the behaviour and performance of Azure Search. Projects exist for populating Azure Search from a SQL Server database and then comparing the performance between the 2 technologies.

## Configuration

### SimpleSearchMVCApp
- Create a file named PrivateSettings.config
- Enter the following content, providing values for the settings
```
<add key="SearchServiceName" value="[SearchInstanceName]" />
<add key="SearchServiceApiKey" value="[SearchApiKey]" />
```

### DataIndexer
- Create a file named PrivateSettings.config
- Enter the following content, providing values for the settings
```
<add key="SearchServiceName" value="[SearchInstanceName]" />
<add key="SearchServiceApiKey" value="[SearchApiKey]" />
<add key="dcfsbatchjobtest_ULN" value="[SqlConnectionString]" />
<add key="SearchGeoNamesIndex" value="[SearchGeoNamesIndex]" />
<add key="SearchUsageDataSource" value="[SearchUsageDataSource]" />
<add key="SearchUsageindexer" value="[SearchUsageindexer]" />
<add key="SearchSqlSourceDescription" value="[SearchSqlSourceDescription]" />
<add key="SearchSqlSourceTableOrView" value="[SearchSqlSourceTableOrView]" />
<add key="SearchSqlSourceConnectionString" value="[SearchSqlSourceConnectionString]" />
```

### SearchVsSql
- Create a file named PrivateSettings.config
- Enter the following content, providing values for the settings
```
<add key="SearchSqlSourceTableOrView" value="[SearchSqlSourceTableOrView]" />
<add key="SearchSqlSourceConnectionString" value="[SearchSqlSourceConnectionString]" />
<add key="dcfsbatchjobtest_Intra" value="[dcfsbatchjobtest_Intra]" />
<add key="RunResultsConnectionString" value="[RunResultsConnectionString]" />
<add key="SearchUsageDataSource" value="[SearchUsageDataSource]" />
```