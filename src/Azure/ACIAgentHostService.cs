
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using Azure.Extensions.Identity;
using FluentAzure = Microsoft.Azure.Management.Fluent.Azure;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

public class ACIAgentHostService : IAgentHostService
{
    private string _jobPrefix;
    private string _azSub;
    private string _azTenant;
    private string _jobImage;
    private string _azResourceGroup;
    private string _azEnvironment;
    private string _orgUrl;
    private string _orgPat;
    private string _os;
    private string _azRegion;
    private IAzure _az;
    public ACIAgentHostService(IConfiguration config)
    {
        _azSub = config.GetValue<string>("AZ_SUBSCRIPTION_ID");
        _azRegion = config.GetValue<string>("AZ_REGION");
        _azTenant = config.GetValue<string>("AZ_TENANT_ID");
        _azResourceGroup = config.GetValue<string>("AZ_RESOURCE_GROUP");
        _jobImage = config.GetValue<string>("JOB_IMAGE");
        _azEnvironment = config.GetValue<string>("AZ_ENVIRONMENT", "AzureGlobalCloud");
        _jobPrefix = config.GetValue<string>("JOB_PREFIX", "agent-job-");
        _orgUrl = config.GetValue<string>("ORG_URL");
        _orgPat = config.GetValue<string>("ORG_PAT");
        _os = config.GetValue<string>("JOB_OS", "linux").ToLower();

        if (_azSub == null) throw new ArgumentNullException("Azure subscription is required.");
        if (_azRegion == null) throw new ArgumentNullException("Azure region is required.");
        if (_azTenant == null) throw new ArgumentNullException("Azure tenant id is required.");
        if (_azResourceGroup == null) throw new ArgumentNullException("Azure resource group is required.");
    }

    private string FormatJobName(long requestId) => $"{_jobPrefix}{requestId}";

    public async Task<bool> IsJobProvisioned(long requestId) =>
        (await GetAzureContext().ContainerGroups.GetByIdAsync(
            $"/subscriptions/{_azSub}/resourceGroups/{_azResourceGroup}/providers/Microsoft.ContainerInstance/containerGroups/{FormatJobName(requestId)}"))
            != null;

    private IAzure GetAzureContext() =>
        FluentAzure
        .Authenticate(new AzureIdentityFluentCredentialAdapter(_azTenant, AzureEnvironment.FromName(_azEnvironment)))
        .WithSubscription(_azSub);

    public async Task StartAgent(long requestId, string agentPool)
    {
        dynamic instance = GetAzureContext()
            .ContainerGroups
            .Define(FormatJobName(requestId))
            .WithRegion(Region.Create(_azRegion))
            .WithExistingResourceGroup(_azResourceGroup);
        
        if (_os == "linux")
        {
            instance = instance.WithLinux();
        }
        else
        {
            instance = instance.WithWindows();
        }

        await instance.WithPublicImageRegistryOnly()
            .WithoutVolume()
            .DefineContainerInstance(FormatJobName(requestId) + "-" + Guid.NewGuid().ToString().Substring(0, 4))
                .WithImage(_jobImage)
                .WithoutPorts()
                .WithEnvironmentVariables(new Dictionary<string, string>()
                {
                    { "AZP_URL", _orgUrl },
                    { "AZP_TOKEN", _orgPat },
                    { "AZP_POOL",  agentPool },
                })
                .Attach()
            .CreateAsync();

    }
    public Task Initialize()
    {
        return Task.CompletedTask;
    }
}
