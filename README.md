# [perfx](https://github.com/vamsitp/perfx)
Azure API Performance benchmarking tool (for *Developers*) based on **App-Insights**

---

#### USAGE
**`perfx`** [`2`] (to override the number of iterations in the _settings_ file)

> Results are saved to your `Documents` folder with the name: **`Perfx_Results.xlsx`**/`Perfx_Results.csv`

  ![Screenshot](Screenshots\Screenshot1.png)
  ![Screenshot2](Screenshots\Screenshot2.png)

- Enter **`r`**`:10` to **run** the benchmarks (`10` times)
- Enter **`s`** to print the **stats**/details for the previous run
- Enter **`l`**`:1h:10` to fetch **app-insights** `duration` **logs** for the previous run (in the last `1 hour` with `10 retries`) 
- Enter **`c`** to **clear** the console
- Enter **`q`** to **quit**
- Enter **`?`** to print this **help**

> **PRE-REQ**: Populate the following JSON and save it to your `Documents` folder with the name: **`Perfx.json`**
> ```json
> {
>     "UserId": "",
>     "Password": "",
>     "Authority": "https://login.microsoftonline.com/YOUR_COMPANY.onmicrosoft.com",
>     "ClientId": "",
>     "ApiScopes": [
>         "api://YOUR-API-SCOPES"
>     ],
>     "AppInsightsAppId": "",
>     "AppInsightsApiKey": ""
>     "Endpoints": [
>         "https://YOUR-API.COM/route1",
>         "https://YOUR-API.COM/route2"
>     ],
>     "Iterations": 5,
>     "OutputFormat": "Excel", // "Csv"
>     "ReadResponseHeadersOnly": false,
>     "InputsFile": "Perfx_Inputs.xlsx", // Headers: semi-colon separated
>     "PluginClassName": null
> }
> ```

> **OPTIONAL**: Populate the following JSON and save it to your `Documents` folder with the name: **`Perfx.Settings.json`**
> ```json
> {
>     "Logging": {
>         "LogLevel": {
>             "Default": "Warning"
>         },
>         "Console": {
>             "IncludeScopes": true,
>             "LogLevel": {
>                 "Default": "Warning" //,"System.Net.Http.HttpClient": "Information"
>             }
>         },
>         "Debug": {
>             "LogLevel": {
>                 "Default": "Information"
>             }
>         }
>     }
> }
> ```

> Also, see [`"allowPublicClient": true`](https://stackoverflow.com/a/57274706)

---
##### [PLUGINS](https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support#simple-plugin-with-no-dependencies)

- Create a *.NET Standard* project and add reference to `Prefx.Core` project
- Add a class that implements `IPlugin` interface
- Update the **`csproj`** file as follows:
  - ```xml
    <ProjectReference Include="Perfx.Core.csproj">
        <Private>false</Private>
        <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
    ```
  - `<TargetFramework>netcoreapp3.1</TargetFramework>`
  - `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` 
  - Build the project and copy the build-output to `Documents/Perfx/Plugins` folder
  - Optionally, if you have multiple `IPlugin` implementations, you can also update the *value* of `PluginClassName` with the specific implementation-class-full-name (e.g. `MyPluginAssembly.MyPlugin1`)

---

```batch
# Install from nuget.org
dotnet tool install -g perfx --no-cache

# Upgrade to latest version from nuget.org
dotnet tool update -g perfx --no-cache

# Install a specific version from nuget.org
dotnet tool install -g perfx --version 1.0.x

# Uninstall
dotnet tool uninstall -g perfx
```
> **NOTE**: If the Tool is not accessible post installation, add `%USERPROFILE%\.dotnet\tools` to the PATH env-var.

##### CONTRIBUTION
```batch

# Install from local project path
dotnet tool install -g --add-source ./bin perfx

# Publish package to nuget.org
nuget push ./bin/Perfx.1.0.0.nupkg -ApiKey <key> -Source https://api.nuget.org/v3/index.json
```

---

[**NOTICES**](./Notices.md)
