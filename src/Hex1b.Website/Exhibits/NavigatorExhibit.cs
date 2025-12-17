using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

#pragma warning disable HEX1B001 // Experimental Navigator API

namespace Hex1b.Website.Exhibits;

/// <summary>
/// A CRM demo using the fluent context-based API demonstrating stack-based navigation.
/// </summary>
public class NavigatorExhibit(ILogger<NavigatorExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<NavigatorExhibit> _logger = logger;

    public override string Id => "navigation";
    public override string Title => "Navigation";
    public override string Description => "A simple CRM system demonstrating stack-based navigation.";

    #region Domain Model

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

    #endregion

    #region Application State

    /// <summary>
    /// All application state in one typed container.
    /// This is the TState for our Hex1bApp&lt;TState&gt;.
    /// </summary>
    private class CrmAppState
    {
        // Domain data
        public List<Customer> Customers { get; } = [];

        // Navigation state
        public NavigatorState Navigator { get; }

        // Form states
        public TextBoxState CompanyNameInput { get; } = new();
        public TextBoxState EmailInput { get; } = new();
        public TextBoxState OpportunityNameInput { get; } = new();
        public TextBoxState OpportunityAmountInput { get; } = new();

        // List states
        public ListState CustomerList { get; } = new();
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

        public CrmAppState()
        {
            // Initialize navigator with startup route
            Navigator = new NavigatorState(new NavigatorRoute("startup", BuildStartup));
        }

        #region Screen Builders using Fluent API

        /// <summary>
        /// Startup - routes to first-run or home based on data.
        /// </summary>
        private Hex1bWidget BuildStartup(NavigatorState nav)
        {
            var ctx = new WidgetContext<RootWidget, CrmAppState>(this);
            
            if (Customers.Count == 0)
            {
                return BuildFirstRun(ctx, nav);
            }
            return BuildHome(ctx, nav);
        }

        /// <summary>
        /// First-run experience using fluent API.
        /// </summary>
        private Hex1bWidget BuildFirstRun(WidgetContext<RootWidget, CrmAppState> ctx, NavigatorState nav)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text("│         Welcome to Mini CRM           │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("This is your first time running the CRM."),
                v.Text(""),
                v.Text("To get started, you'll need to create"),
                v.Text("your first customer record."),
                v.Text(""),
                v.Button("Create First Customer →", () => 
                    nav.Push("new-customer", n => BuildNewCustomer(ctx, n)))
            ]);
        }

        /// <summary>
        /// New customer form using fluent API.
        /// </summary>
        private Hex1bWidget BuildNewCustomer(WidgetContext<RootWidget, CrmAppState> ctx, NavigatorState nav)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text("│          New Customer                 │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("Company Name:"),
                v.TextBox(s => s.CompanyNameInput),
                v.Text(""),
                v.Text("Email:"),
                v.TextBox(s => s.EmailInput),
                v.Text(""),
                v.Button("Save Customer", () => SaveNewCustomer(nav))
            ]).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(() => nav.Pop(), "Cancel");
            });
        }

        private void SaveNewCustomer(NavigatorState nav)
        {
            var customer = new Customer
            {
                CompanyName = CompanyNameInput.Text,
                Email = EmailInput.Text
            };
            Customers.Add(customer);

            // Clear form
            CompanyNameInput.Text = "";
            EmailInput.Text = "";

            // Navigate to home
            nav.Reset(new NavigatorRoute("home", BuildStartup));
        }

        /// <summary>
        /// Home screen - master/detail using fluent API.
        /// </summary>
        private Hex1bWidget BuildHome(WidgetContext<RootWidget, CrmAppState> ctx, NavigatorState nav)
        {
            // Update list from customers
            CustomerList.Items = Customers
                .Select(c => new ListItem(c.Id, c.CompanyName))
                .ToList();

            var selectedCustomer = Customers
                .FirstOrDefault(c => c.Id == CustomerList.SelectedItem?.Id);

            return ctx.Splitter(
                ctx.VStack(left => [
                    left.Text("Customers"),
                    left.Text("─────────────────"),
                    left.List(s => s.CustomerList),
                    left.Text(""),
                    left.Button("+ New", () => 
                        nav.Push("new-customer", n => BuildNewCustomer(ctx, n)))
                ]),
                BuildCustomerDetail(ctx, selectedCustomer, nav),
                leftWidth: 25
            );
        }

        /// <summary>
        /// Customer detail panel (right side of splitter).
        /// </summary>
        private Hex1bWidget BuildCustomerDetail(
            WidgetContext<RootWidget, CrmAppState> ctx,
            Customer? customer,
            NavigatorState nav)
        {
            if (customer == null)
            {
                return ctx.Layout(ctx.VStack(v => [
                    v.Text(""),
                    v.Text("  Select a customer")
                ]));
            }

            // Get opportunity list for this customer
            var oppList = GetOpportunityList(customer.Id);
            oppList.Items = customer.Opportunities
                .Select(o => new ListItem(o.Id, $"{o.Name} - ${o.Amount:N0}"))
                .ToList();

            var totalValue = customer.Opportunities.Sum(o => o.Amount);

            return ctx.Layout(ctx.VStack(v => [
                v.Text("Customer Details"),
                v.Text("────────────────────────"),
                v.Text(""),
                v.Text($"Company: {customer.CompanyName}"),
                v.Text($"Email:   {customer.Email}"),
                v.Text($"ID:      {customer.Id}"),
                v.Text(""),
                v.Text($"Opportunities ({customer.Opportunities.Count}) - Total: ${totalValue:N0}"),
                v.Text("────────────────────────"),
                v.List(_ => oppList),
                v.HStack(h => [
                    h.Button("+ Add", () => 
                        nav.Push("new-opportunity", n => BuildNewOpportunity(ctx, n, customer))),
                    h.Button("Delete", () => 
                        DeleteSelectedOpportunity(customer, oppList))
                ])
            ]));
        }

        /// <summary>
        /// New opportunity form using fluent API.
        /// </summary>
        private Hex1bWidget BuildNewOpportunity(
            WidgetContext<RootWidget, CrmAppState> ctx,
            NavigatorState nav,
            Customer customer)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text($"│  New Opportunity for {customer.CompanyName,-16} │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("Opportunity Name:"),
                v.TextBox(s => s.OpportunityNameInput),
                v.Text(""),
                v.Text("Amount ($):"),
                v.TextBox(s => s.OpportunityAmountInput),
                v.Text(""),
                v.Button("Save Opportunity", () => SaveNewOpportunity(nav, customer))
            ]).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(() => nav.Pop(), "Cancel");
            });
        }

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

            OpportunityNameInput.Text = "";
            OpportunityAmountInput.Text = "";
            nav.Pop();
        }

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

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating widget builder for Navigator CRM demo");

        // Create fresh state for this session
        var state = new CrmAppState();

        return () =>
        {
            try
            {
                _logger.LogDebug("Building NavigatorWidget via fluent API, customer count: {Count}", 
                    state.Customers.Count);
                
                // Create root context and build the navigator widget with info bar
                var ctx = new RootContext<CrmAppState>(state);
                var navigator = ctx.Navigator(s => s.Navigator);
                
                // Build info bar with navigation hints
                var infoBar = ctx.InfoBar([
                    new InfoBarSection(" Mini CRM "),
                    new InfoBarSection(" | "),
                    new InfoBarSection($"Customers: {state.Customers.Count}"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("Tab: Navigate  Enter: Select  Esc: Back")
                ]);
                
                return ctx.VStack(
                    v => [navigator.FillHeight(), infoBar]
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building NavigatorWidget");
                throw;
            }
        };
    }
}
