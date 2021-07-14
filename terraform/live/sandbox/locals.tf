locals {
  shared_tags = (
    var.shared_tags == null
    ? {
      "Owner"       = var.owner
      "DevelopedBy" = "terraform"
      "Environment" = var.env
    }
    : var.shared_tags
  )
}
