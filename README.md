# Perfx
Generate Excel dump of Azure Resources
![image.png](Screenshot.png)

---

#### USAGE
`Perfx`

- Enter **`r`** to run the performance benchmarks
- Enter **`f`** to process offline **files** (JSON downloaded from `https://resources.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupId}/resources`)
- Enter **`c`** to **clear** the console
- Enter **`q`** to **quit**
- Enter **`?`** to print this **help**

---

```batch
# Install from nuget.org
dotnet tool install -g Perfx

# Upgrade to latest version from nuget.org
dotnet tool update -g Perfx --no-cache

# Install a specific version from nuget.org
dotnet tool install -g Perfx --version 1.0.x

# Uninstall
dotnet tool uninstall -g Perfx
```
> **NOTE**: If the Tool is not accessible post installation, add `%USERPROFILE%\.dotnet\tools` to the PATH env-var.

##### CONTRIBUTION
```batch

# Install from local project path
dotnet tool install -g --add-source ./bin Perfx

# Publish package to nuget.org
nuget push ./bin/Perfx.1.0.0.nupkg -ApiKey <key> -Source https://api.nuget.org/v3/index.json
```