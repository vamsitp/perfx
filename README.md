# [perfx](https://github.com/vamsitp/perfx)
Azure API Performance benchmarking tool based on **App-Insights**

---

#### USAGE
**`perfx`** [`2`] (to override the number of iterations in the _settings_ file)

> Results are saved to your `Documents` folder with the name: **`Perfx.csv`**

  ![Screenshot](Screenshot.png)
  ![Screenshot2](Screenshot2.png)

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
>     "Endpoints": [
>         "https://YOUR-API.COM/route1",
>         "https://YOUR-API.COM/route2"
>     ],
>     "Iterations": 5,
>     "OutputFormat": "Excel", // Csv
>     "ReadResponseHeadersOnly": false,
>     "AppInsightsAppId": "",
>     "AppInsightsApiKey": ""
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