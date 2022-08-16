#!/bin/bash
rg=azure-container-app-test
app=producer-containerapp-internal
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple
az containerapp revision set-mode -n $app -g $rg --mode single
az containerapp revision set-mode -n $app -g $rg --mode multiple