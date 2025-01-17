using Microsoft.Extensions.Configuration;
using k8s;
using k8s.Models;

public class KubernetesAgentHostService : IAgentHostService
{
    private const string DEFAULT_JOB_PREFIX = "agent-job-";
    private string _namespace;
    private string _jobPrefix;
    private string _jobImage;
    private string _azpUrl;
    private string _azpPat;
    private string _dockerSockPath;
    private string _jobDefFile;
    private V1Job _predefinedJob;
    private Kubernetes _kubectl;

    private string FormatJobName(long requestId) => $"{_jobPrefix}-{requestId}";
    

    public KubernetesAgentHostService(IConfiguration config)
    {
        // The default config will either check the well known KUBECONFIG env
        // or, if within the cluster, checks /var/run/secrets/kubernetes.io/serviceaccount
        _kubectl = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        _namespace = config.GetValue<string>("JOB_NAMESPACE", "default");
        _jobPrefix = config.GetValue<string>("JOB_PREFIX", DEFAULT_JOB_PREFIX);
        _jobImage = config.GetValue<string>("JOB_IMAGE");
        _azpUrl = config.GetValue<string>("ORG_URL");
        _azpPat = config.GetValue<string>("ORG_PAT");
        _dockerSockPath = config.GetValue<string>("JOB_DOCKER_SOCKET_PATH");
        _jobDefFile = config.GetValue<string>("JOB_DEFINITION_FILE");

        if (_jobDefFile != null)
        {
            _predefinedJob = KubernetesYaml.Deserialize<V1Job>(System.IO.File.ReadAllText(_jobDefFile));
            _jobPrefix = _predefinedJob.Metadata.Name ?? _jobPrefix;
        }
    }

    public async Task Initialize()
    {
        if (!(await _kubectl.CoreV1.ListNamespaceAsync()).Items.Any(ns => ns.Name() == _namespace))
        {
            await _kubectl.CoreV1.CreateNamespaceAsync(new V1Namespace()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = _namespace
                }
            });
        }
    }

    public async Task<bool> IsJobProvisioned(long requestId) =>
        (await _kubectl.ListNamespacedJobAsync(_namespace))
            .Items.Any(x => x.Metadata.Name == FormatJobName(requestId));

    public async Task StartAgent(long requestId, string agentPool)
    {
        V1Job job = _predefinedJob;
        if (_predefinedJob != null)
        {
            _predefinedJob.Metadata.Name = FormatJobName(requestId);
        }
        else
        {
            job = new V1Job()
            {
                ApiVersion = "batch/v1",
                Kind = "Job",
                Metadata = new V1ObjectMeta()
                {
                    Name = FormatJobName(requestId),
                    NamespaceProperty = _namespace
                },
                Spec = new V1JobSpec()
                {
                    Template = new V1PodTemplateSpec()
                    {
                        Spec = new V1PodSpec()
                        {
                            RestartPolicy = "Never",
                            Containers = new List<V1Container>(){
                            new V1Container() {
                                Name = FormatJobName(requestId),
                                Image = _jobImage,
                                Env = new List<V1EnvVar>() {
                                    new V1EnvVar() {
                                        Name = "AZP_URL",
                                        Value = _azpUrl
                                    },
                                    new V1EnvVar() {
                                        Name = "AZP_TOKEN",
                                        Value = _azpPat
                                    },
                                    new V1EnvVar() {
                                        Name = "AZP_POOL",
                                        Value = agentPool
                                    }
                                }
                            }
                        }
                        }
                    }
                }

            };


            // Not all K8s environments support the docker.sock
            // This option allows users to opt-in to adding the volume mount
            if (_dockerSockPath != null)
            {
                var podSpec = job.Spec.Template.Spec;
                var volumeName = "docker-volume";

                podSpec.Volumes = new List<V1Volume>(){
                new V1Volume() {
                    Name = volumeName,
                    HostPath = new V1HostPathVolumeSource() {
                        Path = _dockerSockPath
                    }
                }
            };

                podSpec.Containers[0].VolumeMounts = new List<V1VolumeMount>() {
                new V1VolumeMount() {
                    MountPath = _dockerSockPath,
                    Name = volumeName
                }
            };
            }
        }
        await _kubectl.CreateNamespacedJobAsync(job, namespaceParameter: _namespace);
    }
}