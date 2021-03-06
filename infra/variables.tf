variable "location" {
  type    = string
  default = "West Europe"
}

variable "producer_image_name" {
  type    = string
  default = "producer"
}

variable "consumer_image_name" {
  type    = string
  default = "consumer"
}

variable "healthprobeinvoker_image_name" {
  type    = string
  default = "warmup"
}

variable "environment" {
  type    = string
  default = "dev"
}