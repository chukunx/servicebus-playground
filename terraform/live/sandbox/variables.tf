variable "shared_tags" {
  type        = map(string)
  default     = null
  description = "shared tags"
}

variable "owner" {
  type = string
}

variable "env" {
  type = string
}

variable "region_short" {
  type = string
}

variable "region" {
  type = string
}

variable "resource_type" {
  type = string
}

variable "switch_sb" {
  type = bool
}

variable "switch_signalr" {
  type = bool
}
