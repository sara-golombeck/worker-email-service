using Amazon.SQS;
using Amazon.SimpleEmail;
using EmailWorker.Services;
using Prometheus;

var builder = Host.CreateApplicationBuilder(args);

// Add AWS services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonSimpleEmailService>();

// Add custom services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<EmailWorkerService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();

// Start Prometheus metrics server
var metricServer = new MetricServer(hostname: "*", port: 8080);
metricServer.Start();

// Set initial health status
WorkerMetrics.WorkerHealth.Set(1);

try
{
    // Run the worker
    await host.RunAsync();
}
finally
{
    WorkerMetrics.WorkerHealth.Set(0);
    metricServer.Stop();
}