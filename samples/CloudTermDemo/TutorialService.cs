namespace CloudTermDemo;

/// <summary>
/// Tracks tutorial progress and provides markdown content for each step.
/// </summary>
public sealed class TutorialService
{
    private static readonly string[] Steps =
    [
        // Step 0: Welcome
        """
        # Welcome to Cloud Term

        This tutorial will guide you through the basics of using
        the Cloud Terminal.

        The panel on the left is your **cloud shell**. You can
        type commands there to explore your cloud resources.

        Try typing `ls` and pressing **Enter** to see what's
        available.
        """,

        // Step 1: Navigating
        """
        # Navigating Resources

        Great! You can see the items at your current level.

        Use `cd <name>` to navigate into a resource, and
        `cd ..` to go back up.

        Try navigating into a tenant:
        ```
        cd Contoso Corp
        ```

        Then use `ls` to see its subscriptions.
        """,

        // Step 2: Exploring
        """
        # Exploring Deeper

        You can keep drilling down through the hierarchy:

        - **Tenants** contain subscriptions
        - **Subscriptions** contain resource groups
        - **Resource Groups** contain resources
        - Some **resources** (like AKS) have sub-resources

        Try navigating all the way to a resource:
        ```
        cd Production
        cd rg-web-prod
        ls
        ```
        """,

        // Step 3: Panel controls
        """
        # Panel Controls

        You can manage the panels in your terminal:

        - **Ctrl+←/→** — resize panels
        - **Tab** — switch focus between panels
        - **F1** — toggle help

        Try pressing **Tab** to switch to this tutorial panel,
        then **Tab** again to go back to the shell.
        """,

        // Step 4: Done
        """
        # You're All Set!

        You now know the basics of Cloud Term:

        - `ls` — list resources at current level
        - `cd <name>` — navigate into a resource
        - `cd ..` — go back up
        - `cd /` — go to root
        - **Tab** — switch panels
        - **F1** — help

        Happy cloud exploring! 🌥️
        """,
    ];

    public int CurrentStep { get; private set; }

    public int TotalSteps => Steps.Length;

    public string GetCurrentMarkdown() => Steps[CurrentStep];

    public bool CanAdvance => CurrentStep < Steps.Length - 1;

    public void Advance()
    {
        if (CanAdvance)
            CurrentStep++;
    }

    public void Reset() => CurrentStep = 0;
}
