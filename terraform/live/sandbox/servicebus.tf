resource "azurerm_servicebus_namespace" "sb_namespace" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sb-${var.resource_type}-${var.env}-${var.region_short}-01"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "Standard"

  tags = merge(local.shared_tags, {})
}

# Topics
resource "azurerm_servicebus_topic" "relay_topic1" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name

  max_size_in_megabytes = 5120
  enable_partitioning   = false

  depends_on = [
    azurerm_servicebus_namespace.sb_namespace[0]
  ]
}

resource "azurerm_servicebus_topic" "sequenced_topic1" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sequenced_topic1"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name

  max_size_in_megabytes = 5120
  enable_partitioning   = false

  depends_on = [
    azurerm_servicebus_namespace.sb_namespace[0]
  ]
}

# Subscriptions
resource "azurerm_servicebus_subscription" "relay_topic1_red" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_red"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  max_delivery_count  = 1

  depends_on = [
    azurerm_servicebus_topic.relay_topic1[0]
  ]
}

resource "azurerm_servicebus_subscription" "relay_topic1_red_fwd" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_red_fwd"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  max_delivery_count  = 1
  forward_to          = "sequenced_topic1" # forward to sequenced topic

  depends_on = [
    azurerm_servicebus_topic.relay_topic1[0],
    azurerm_servicebus_topic.sequenced_topic1[0]
  ]
}

resource "azurerm_servicebus_subscription" "relay_topic1_cyan" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_cyan"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  max_delivery_count  = 1

  depends_on = [
    azurerm_servicebus_topic.relay_topic1[0]
  ]
}

resource "azurerm_servicebus_subscription" "relay_topic1_cyan_fwd" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_cyan_fwd"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  max_delivery_count  = 1
  forward_to          = "sequenced_topic1" # forward to sequenced topic

  depends_on = [
    azurerm_servicebus_topic.relay_topic1[0],
    azurerm_servicebus_topic.sequenced_topic1[0]
  ]
}

resource "azurerm_servicebus_subscription" "sequenced_topic1_red" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sequenced_topic1_red"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "sequenced_topic1"
  requires_session    = true # session enabled subscription
  max_delivery_count  = 3    # deferal would not work properly if set to 1

  depends_on = [
    azurerm_servicebus_topic.sequenced_topic1[0]
  ]
}

resource "azurerm_servicebus_subscription" "sequenced_topic1_cyan" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sequenced_topic1_cyan"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "sequenced_topic1"
  requires_session    = true # session enabled subscription
  max_delivery_count  = 3    # deferal would not work properly if set to 1

  depends_on = [
    azurerm_servicebus_topic.sequenced_topic1[0]
  ]
}

# SqlFilter
resource "azurerm_servicebus_subscription_rule" "relay_topic1_red_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_red_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  subscription_name   = "relay_topic1_red"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'red' AND NOT EXISTS(SessionName)"

  depends_on = [
    azurerm_servicebus_subscription.relay_topic1_cyan
  ]
}

resource "azurerm_servicebus_subscription_rule" "relay_topic1_red_fwd_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_red_fwd_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  subscription_name   = "relay_topic1_red_fwd"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'red' AND EXISTS(SessionName) AND EXISTS(TransactionId)"

  depends_on = [
    azurerm_servicebus_subscription.relay_topic1_red_fwd
  ]
}

resource "azurerm_servicebus_subscription_rule" "relay_topic1_cyan_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_cyan_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  subscription_name   = "relay_topic1_cyan"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'cyan' AND NOT EXISTS(SessionName)"

  depends_on = [
    azurerm_servicebus_subscription.relay_topic1_cyan
  ]
}

resource "azurerm_servicebus_subscription_rule" "relay_topic1_cyan_fwd_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "relay_topic1_cyan_fwd_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "relay_topic1"
  subscription_name   = "relay_topic1_cyan_fwd"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'cyan' AND EXISTS(SessionName) AND EXISTS(TransactionId)"

  depends_on = [
    azurerm_servicebus_subscription.relay_topic1_cyan_fwd
  ]
}

resource "azurerm_servicebus_subscription_rule" "sequenced_topic1_red_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sequenced_topic1_red_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "sequenced_topic1"
  subscription_name   = "sequenced_topic1_red"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'red' AND EXISTS(SessionName) AND EXISTS(TransactionId)"
  action              = "SET sys.SessionId = SessionName + '_' + TransactionId + '_' + InstanceId"

  depends_on = [
    azurerm_servicebus_subscription.sequenced_topic1_cyan
  ]
}

resource "azurerm_servicebus_subscription_rule" "sequenced_topic1_cyan_rule" {
  count               = (var.switch_sb ? 1 : 0)
  name                = "sequenced_topic1_cyan_rule"
  resource_group_name = azurerm_resource_group.rg.name
  namespace_name      = azurerm_servicebus_namespace.sb_namespace[0].name
  topic_name          = "sequenced_topic1"
  subscription_name   = "sequenced_topic1_cyan"
  filter_type         = "SqlFilter"
  sql_filter          = "color = 'cyan' AND EXISTS(SessionName) AND EXISTS(TransactionId)"
  action              = "SET sys.SessionId = SessionName + '_' + TransactionId + '_' + InstanceId"

  depends_on = [
    azurerm_servicebus_subscription.sequenced_topic1_cyan
  ]
}
