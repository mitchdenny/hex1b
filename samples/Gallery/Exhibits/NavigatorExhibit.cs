using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

#pragma warning disable HEX1B001 // Experimental Navigator API

namespace Gallery.Exhibits;

public class NavigatorExhibit(ILogger<NavigatorExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<NavigatorExhibit> _logger = logger;

    public override string Id => "navigator";
    public override string Title => "Navigator - CRM Demo";
    public override string Description => "A simple CRM system demonstrating stack-based navigation.";

    public override string SourceCode => """
        // CRM State
        var crm = new CrmState();
        
        // Navigator with startup logic
        var nav = new NavigatorState(
            new NavigatorRoute("startup", n => Startup(n, crm))
        );
        
        return new NavigatorWidget(nav);
        """;

    #region CRM Domain Model

    private class Opportunity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
    }

    private class Customer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string CompanyName { get; set; } = "";
        public string Email { get; set; } = "";
        public List<Opportunity> Opportunities { get; } = [];
    }

    private class CrmState
    {
        public List<Customer> Customers { get; } = [];
    }

    #endregion

    #region Session State

    private class CrmSessionState
    {
        public CrmState Crm { get; } = new();
        public NavigatorState Navigator { get; }

        // Form state for new customer
        public TextBoxState CompanyNameInput { get; } = new();
        public TextBoxState EmailInput { get; } = new();

        // List state for customer list
        public ListState CustomerList { get; } = new();

        // Opportunity form state
        public TextBoxState OpportunityNameInput { get; } = new();
        public TextBoxState OpportunityAmountInput { get; } = new();

        // Opportunity list state (per-customer, keyed by customer ID)
        private readonly Dictionary<string, ListState> _opportunityLists = new();

        public ListState GetOpportunityList(string customerId)
        {
            if (!_opportunityLists.TryGetValue(customerId, out var list))
            {
                list = new ListState();
                _opportunityLists[customerId] = list;
            }
            return list;
        }

        public CrmSessionState()
        {
            Navigator = new NavigatorState(new NavigatorRoute("startup", Startup));
        }

        #region Screens

        /// <summary>
        /// Startup screen - decides whether to show first-run or home.
        /// </summary>
        private Hex1bWidget Startup(NavigatorState nav)
        {
            if (Crm.Customers.Count == 0)
            {
                return FirstRun(nav);
            }
            else
            {
                return Home(nav);
            }
        }

        /// <summary>
        /// First-run experience - shown when there are no customers.
        /// </summary>
        private Hex1bWidget FirstRun(NavigatorState nav)
        {
            return new VStackWidget(
            [
                new TextBlockWidget("╭───────────────────────────────────────╮"),
                new TextBlockWidget("│         Welcome to Mini CRM           │"),
                new TextBlockWidget("╰───────────────────────────────────────╯"),
                new TextBlockWidget(""),
                new TextBlockWidget("This is your first time running the CRM."),
                new TextBlockWidget(""),
                new TextBlockWidget("To get started, you'll need to create"),
                new TextBlockWidget("your first customer record."),
                new TextBlockWidget(""),
                new ButtonWidget("Create First Customer →", () => nav.Push("new-customer", NewCustomer)),
            ],
            [
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content
            ]);
        }

        /// <summary>
        /// New customer form.
        /// </summary>
        private Hex1bWidget NewCustomer(NavigatorState nav)
        {
            return new VStackWidget(
            [
                new TextBlockWidget("╭───────────────────────────────────────╮"),
                new TextBlockWidget("│          New Customer                 │"),
                new TextBlockWidget("╰───────────────────────────────────────╯"),
                new TextBlockWidget(""),
                new TextBlockWidget("Company Name:"),
                new TextBoxWidget(CompanyNameInput),
                new TextBlockWidget(""),
                new TextBlockWidget("Email:"),
                new TextBoxWidget(EmailInput),
                new TextBlockWidget(""),
                new ButtonWidget("Save Customer", () => SaveNewCustomer(nav)),
            ],
            [
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content
            ]) 
            { 
                Shortcuts = [new Shortcut(KeyBinding.Plain(ConsoleKey.Escape), () => nav.Pop(), "Cancel")] 
            };
        }

        /// <summary>
        /// Save the new customer and navigate to home.
        /// </summary>
        private void SaveNewCustomer(NavigatorState nav)
        {
            var customer = new Customer
            {
                CompanyName = CompanyNameInput.Text,
                Email = EmailInput.Text
            };
            Crm.Customers.Add(customer);

            // Clear form for next time
            CompanyNameInput.Text = "";
            EmailInput.Text = "";

            // Replace entire stack with Home (completing the first-run flow)
            nav.Reset(new NavigatorRoute("home", Home));
        }

        /// <summary>
        /// Home screen - master/detail view of all customers.
        /// </summary>
        private Hex1bWidget Home(NavigatorState nav)
        {
            // Update list items from customers
            CustomerList.Items = Crm.Customers
                .Select(c => new ListItem(c.Id, c.CompanyName))
                .ToList();

            var selectedCustomer = Crm.Customers
                .FirstOrDefault(c => c.Id == CustomerList.SelectedItem?.Id);

            return new SplitterWidget(
                Left: new VStackWidget(
                [
                    new TextBlockWidget("Customers"),
                    new TextBlockWidget("─────────────────"),
                    new ListWidget(CustomerList),
                    new TextBlockWidget(""),
                    new ButtonWidget("+ New", () => nav.Push("new-customer", NewCustomer)),
                ],
                [
                    SizeHint.Content, SizeHint.Content, SizeHint.Fill, 
                    SizeHint.Content, SizeHint.Content
                ]),
                Right: CustomerDetail(selectedCustomer, nav),
                LeftWidth: 25
            );
        }

        /// <summary>
        /// Customer detail panel (right side of master/detail).
        /// </summary>
        private Hex1bWidget CustomerDetail(Customer? customer, NavigatorState nav)
        {
            if (customer == null)
            {
                return new VStackWidget(
                [
                    new TextBlockWidget(""),
                    new TextBlockWidget("  Select a customer"),
                ]);
            }

            // Get or create list state for this customer's opportunities
            var oppList = GetOpportunityList(customer.Id);
            oppList.Items = customer.Opportunities
                .Select(o => new ListItem(o.Id, $"{o.Name} - ${o.Amount:N0}"))
                .ToList();

            var totalValue = customer.Opportunities.Sum(o => o.Amount);

            return new VStackWidget(
            [
                new TextBlockWidget("Customer Details"),
                new TextBlockWidget("────────────────────────"),
                new TextBlockWidget(""),
                new TextBlockWidget($"Company: {customer.CompanyName}"),
                new TextBlockWidget($"Email:   {customer.Email}"),
                new TextBlockWidget($"ID:      {customer.Id}"),
                new TextBlockWidget(""),
                new TextBlockWidget($"Opportunities ({customer.Opportunities.Count}) - Total: ${totalValue:N0}"),
                new TextBlockWidget("────────────────────────"),
                new ListWidget(oppList),
                new HStackWidget(
                [
                    new ButtonWidget("+ Add", () => nav.Push("new-opportunity", n => NewOpportunity(n, customer))),
                    new ButtonWidget("Delete", () => DeleteSelectedOpportunity(customer, oppList)),
                ],
                [
                    SizeHint.Content, SizeHint.Content
                ]),
            ],
            [
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Fill, SizeHint.Content
            ]);
        }

        /// <summary>
        /// New opportunity form.
        /// </summary>
        private Hex1bWidget NewOpportunity(NavigatorState nav, Customer customer)
        {
            return new VStackWidget(
            [
                new TextBlockWidget("╭───────────────────────────────────────╮"),
                new TextBlockWidget($"│  New Opportunity for {customer.CompanyName,-16} │"),
                new TextBlockWidget("╰───────────────────────────────────────╯"),
                new TextBlockWidget(""),
                new TextBlockWidget("Opportunity Name:"),
                new TextBoxWidget(OpportunityNameInput),
                new TextBlockWidget(""),
                new TextBlockWidget("Amount ($):"),
                new TextBoxWidget(OpportunityAmountInput),
                new TextBlockWidget(""),
                new ButtonWidget("Save Opportunity", () => SaveNewOpportunity(nav, customer)),
            ],
            [
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content, SizeHint.Content,
                SizeHint.Content, SizeHint.Content
            ])
            {
                Shortcuts = [new Shortcut(KeyBinding.Plain(ConsoleKey.Escape), () => nav.Pop(), "Cancel")]
            };
        }

        /// <summary>
        /// Save the new opportunity and return to home.
        /// </summary>
        private void SaveNewOpportunity(NavigatorState nav, Customer customer)
        {
            if (decimal.TryParse(OpportunityAmountInput.Text, out var amount))
            {
                var opportunity = new Opportunity
                {
                    Name = OpportunityNameInput.Text,
                    Amount = amount
                };
                customer.Opportunities.Add(opportunity);
            }

            // Clear form for next time
            OpportunityNameInput.Text = "";
            OpportunityAmountInput.Text = "";

            nav.Pop();
        }

        /// <summary>
        /// Delete the selected opportunity from a customer.
        /// </summary>
        private static void DeleteSelectedOpportunity(Customer customer, ListState oppList)
        {
            var selectedId = oppList.SelectedItem?.Id;
            if (selectedId == null) return;

            var opp = customer.Opportunities.FirstOrDefault(o => o.Id == selectedId);
            if (opp != null)
            {
                customer.Opportunities.Remove(opp);
            }
        }

        #endregion
    }

    #endregion

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating widget builder for Navigator CRM demo");
        
        // Create fresh state for this session - captured in closure
        var sessionState = new CrmSessionState();

        return ct =>
        {
            try
            {
                _logger.LogDebug("Building NavigatorWidget, customer count: {Count}", sessionState.Crm.Customers.Count);
                return Task.FromResult<Hex1bWidget>(new NavigatorWidget(sessionState.Navigator));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building NavigatorWidget");
                throw;
            }
        };
    }
}
