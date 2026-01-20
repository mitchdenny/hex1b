using System.Xml.Linq;
using Octokit;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new InvalidOperationException("GITHUB_TOKEN environment variable is required");
var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? throw new InvalidOperationException("GITHUB_REPOSITORY environment variable is required");
var workflowRunUrl = Environment.GetEnvironmentVariable("WORKFLOW_RUN_URL") ?? throw new InvalidOperationException("WORKFLOW_RUN_URL environment variable is required");

var repoParts = repository.Split('/');
if (repoParts.Length != 2)
{
    throw new InvalidOperationException($"Invalid repository format: {repository}. Expected: owner/repo");
}

var owner = repoParts[0];
var repo = repoParts[1];

// Find TRX file
var trxFiles = Directory.GetFiles("./TestResults", "*.trx", SearchOption.AllDirectories);
if (trxFiles.Length == 0)
{
    Console.WriteLine("No TRX file found!");
    return 1;
}

var trxFile = trxFiles[0];
Console.WriteLine($"Processing test results from: {trxFile}");

// Parse TRX file
XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var doc = XDocument.Load(trxFile);

var failedTests = doc.Descendants(ns + "UnitTestResult")
    .Where(x => x.Attribute("outcome")?.Value == "Failed")
    .Select(x => x.Attribute("testName")?.Value)
    .Where(x => !string.IsNullOrEmpty(x))
    .ToList();

if (failedTests.Count == 0)
{
    Console.WriteLine("No failed tests found. All tests passed!");
    return 0;
}

Console.WriteLine($"Found {failedTests.Count} failed test(s):");
foreach (var test in failedTests)
{
    Console.WriteLine($"  - {test}");
}

// Initialize GitHub client
var client = new GitHubClient(new ProductHeaderValue("hex1b-daily-tests"))
{
    Credentials = new Credentials(token)
};

var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

// Process each failed test
foreach (var testName in failedTests)
{
    Console.WriteLine($"\nProcessing failed test: {testName}");
    
    var issueTitle = $"[Daily Test] Failing: {testName}";
    
    // Search for existing open issues using GitHub search
    var openSearchRequest = new SearchIssuesRequest(issueTitle)
    {
        Repos = new RepositoryCollection { { owner, repo } },
        State = ItemState.Open,
        Type = IssueTypeQualifier.Issue,
        In = new[] { IssueInQualifier.Title }
    };
    
    var openResults = await client.Search.SearchIssues(openSearchRequest);
    
    // Handle pagination for open issues
    var allOpenIssues = openResults.Items.ToList();
    var currentPage = 1;
    while (allOpenIssues.Count < openResults.TotalCount)
    {
        currentPage++;
        openSearchRequest.Page = currentPage;
        openResults = await client.Search.SearchIssues(openSearchRequest);
        allOpenIssues.AddRange(openResults.Items);
    }
    
    var existingOpenIssue = allOpenIssues.FirstOrDefault(i => i.Title.Equals(issueTitle, StringComparison.Ordinal));
    
    if (existingOpenIssue != null)
    {
        Console.WriteLine($"Found existing open issue #{existingOpenIssue.Number} for {testName}");
        
        var commentBody = $@"Test is still failing in daily test run.

**Failed on:** {timestamp}
**Workflow run:** {workflowRunUrl}";
        
        await client.Issue.Comment.Create(owner, repo, existingOpenIssue.Number, commentBody);
        Console.WriteLine($"Added comment to issue #{existingOpenIssue.Number}");
    }
    else
    {
        Console.WriteLine("No open issue found. Checking for closed issues...");
        
        // Search for closed issues
        var closedSearchRequest = new SearchIssuesRequest(issueTitle)
        {
            Repos = new RepositoryCollection { { owner, repo } },
            State = ItemState.Closed,
            Type = IssueTypeQualifier.Issue,
            In = new[] { IssueInQualifier.Title }
        };
        
        var closedResults = await client.Search.SearchIssues(closedSearchRequest);
        
        // Handle pagination for closed issues
        var allClosedIssues = closedResults.Items.ToList();
        currentPage = 1;
        while (allClosedIssues.Count < closedResults.TotalCount)
        {
            currentPage++;
            closedSearchRequest.Page = currentPage;
            closedResults = await client.Search.SearchIssues(closedSearchRequest);
            allClosedIssues.AddRange(closedResults.Items);
        }
        
        var existingClosedIssue = allClosedIssues.FirstOrDefault(i => i.Title.Equals(issueTitle, StringComparison.Ordinal));
        
        if (existingClosedIssue != null)
        {
            Console.WriteLine($"Found closed issue #{existingClosedIssue.Number}. Creating new issue with reference...");
            
            var issueBody = $@"Test **{testName}** is failing again in daily test monitoring.

**Failed on:** {timestamp}
**Workflow run:** {workflowRunUrl}

---

This issue was previously tracked in #{existingClosedIssue.Number}";
            
            var newIssue = new NewIssue(issueTitle)
            {
                Body = issueBody
            };
            newIssue.Labels.Add("test-failure");
            newIssue.Labels.Add("daily-monitoring");
            
            var createdIssue = await client.Issue.Create(owner, repo, newIssue);
            Console.WriteLine($"Created new issue #{createdIssue.Number}");
        }
        else
        {
            Console.WriteLine("No existing issues found. Creating new issue...");
            
            var issueBody = $@"Test **{testName}** is failing in daily test monitoring.

**Failed on:** {timestamp}
**Workflow run:** {workflowRunUrl}";
            
            var newIssue = new NewIssue(issueTitle)
            {
                Body = issueBody
            };
            newIssue.Labels.Add("test-failure");
            newIssue.Labels.Add("daily-monitoring");
            
            var createdIssue = await client.Issue.Create(owner, repo, newIssue);
            Console.WriteLine($"Created new issue #{createdIssue.Number}");
        }
    }
}

Console.WriteLine("\nFinished processing failed tests.");
return 0;
