MAKEFLAGS += --warn-undefined-variables
SHELL := /bin/bash
.SHELLFLAGS := -eu -o pipefail -c

export

export PUID ?= $(shell id -u)
export PGID ?= $(shell id -g)

DOCKER := docker
ECHO := echo
CONTAINER_CS2 := cs2mm

CS2_ENV_FILE ?= ../../.env
CS2_SLOT_MK := ../../make/cs2-slot.mk
ifneq ($(wildcard $(CS2_SLOT_MK)),)
include $(CS2_SLOT_MK)
ifeq ($(wildcard $(CS2_ENV_FILE)),)
  DOCKER_COMPOSE := $(CS2_COMPOSE_ENV) docker compose -f ../../docker-compose.yml
else
  DOCKER_COMPOSE := $(CS2_COMPOSE_ENV) docker compose --env-file $(CS2_ENV_FILE) -f ../../docker-compose.yml
endif
else
CS2_LATEST_GAME_DIR :=
CS2_MAKE_HELP = awk -F':.*\#\# ' '/^[A-Za-z0-9][A-Za-z0-9_.-]*:.*\#\# / { if (!seen[$$1]++) { gsub(/\#/, " ", $$2); printf "  \033[1;32m%-36s\033[0m %s\n", $$1, $$2 } }' $(MAKEFILE_LIST)
.DEFAULT_GOAL := help
endif

DOTNET_SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:8.0
NUGET_CACHE ?= /tmp/css-plugin-nuget-$(shell id -u)
PUBLISH_DIR := .publish
ADDONS_DIR ?= ../addons
PLUGIN_DEST := $(ADDONS_DIR)/counterstrikesharp/plugins/CS2SP

all: help
.PHONY: all

help:
	@$(ECHO) -e "\e[32m Usage: make [target] "
	@$(ECHO)
	@$(ECHO) -e "\e[1m targets:\e[0m"
	@$(CS2_MAKE_HELP)
.PHONY: help

# -------------------------------------------------------
# Compile / test (always Docker — host may not have dotnet)
# -------------------------------------------------------

compile: ## Publish CS2SP.dll via the .NET 8 SDK image → .publish/
	@command -v docker >/dev/null || { echo -e "\e[0;31m ERROR: docker is required\e[0m"; exit 1; }
	@$(ECHO) -e "\e[1m Publishing CS2SP CSS plugin (net8.0)...\e[0m"
	@build="$$(mktemp -d /tmp/cs2sp-css-build-XXXXXX)"; \
	mkdir -p "$(NUGET_CACHE)"; \
	cp -rf ./ "$$build/"; \
	rm -rf "$$build/CS2SP/bin" "$$build/CS2SP/obj" "$$build/CS2SP.Logic/bin" "$$build/CS2SP.Logic/obj" \
		"$$build/CS2SP.Tests/bin" "$$build/CS2SP.Tests/obj" "$$build/$(PUBLISH_DIR)"; \
	if ! $(DOCKER) run --rm \
		--user "$$(id -u):$$(id -g)" \
		-e HOME=/tmp \
		-e DOTNET_CLI_HOME=/tmp \
		-e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
		-e NUGET_PACKAGES=/nuget \
		-v "$$build:/src" \
		-v "$(NUGET_CACHE):/nuget" \
		-w /src \
		$(DOTNET_SDK_IMAGE) \
		dotnet publish CS2SP/CS2SP.csproj -c Release -o /src/$(PUBLISH_DIR); then \
		rm -rf "$$build"; \
		exit 1; \
	fi; \
	if [ ! -f "$$build/$(PUBLISH_DIR)/CS2SP.dll" ]; then \
		echo -e "\e[0;31m ERROR: publish did not produce CS2SP.dll\e[0m"; \
		rm -rf "$$build"; \
		exit 1; \
	fi; \
	rm -rf "$(PUBLISH_DIR)"; \
	cp -a "$$build/$(PUBLISH_DIR)" "$(PUBLISH_DIR)"; \
	rm -rf "$$build"; \
	$(ECHO) -e "\e[1m Published $(PUBLISH_DIR)/CS2SP.dll\e[0m"
.PHONY: compile

test: ## Run CS2SP.Logic xUnit tests in the .NET 8 SDK image
	@command -v docker >/dev/null || { echo -e "\e[0;31m ERROR: docker is required\e[0m"; exit 1; }
	@$(ECHO) -e "\e[1m Testing CS2SP.Logic...\e[0m"
	@build="$$(mktemp -d /tmp/cs2sp-css-test-XXXXXX)"; \
	mkdir -p "$(NUGET_CACHE)"; \
	cp -rf ./ "$$build/"; \
	rm -rf "$$build/CS2SP/bin" "$$build/CS2SP/obj" "$$build/CS2SP.Logic/bin" "$$build/CS2SP.Logic/obj" \
		"$$build/CS2SP.Tests/bin" "$$build/CS2SP.Tests/obj"; \
	$(DOCKER) run --rm \
		--user "$$(id -u):$$(id -g)" \
		-e HOME=/tmp \
		-e DOTNET_CLI_HOME=/tmp \
		-e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
		-e NUGET_PACKAGES=/nuget \
		-v "$$build:/src" \
		-v "$(NUGET_CACHE):/nuget" \
		-w /src \
		$(DOTNET_SDK_IMAGE) \
		dotnet test CS2SP.Tests/CS2SP.Tests.csproj -c Release --nologo; \
	status=$$?; \
	rm -rf "$$build"; \
	exit $$status
.PHONY: test

copy-addons: ## Install .publish/ into ADDONS_DIR (default ../addons) and stash Metamod cs2sp.vdf
	@if [ ! -f "$(PUBLISH_DIR)/CS2SP.dll" ]; then \
		echo -e "\e[0;31m ERROR: $(PUBLISH_DIR)/CS2SP.dll missing. Run: make compile\e[0m"; \
		exit 1; \
	fi
	@$(ECHO) -e "\e[1m Deploying CSS CS2SP to $(PLUGIN_DEST)\e[0m"
	@rm -rf "$(PLUGIN_DEST)"
	@mkdir -p "$(PLUGIN_DEST)"
	@cp -f "$(PUBLISH_DIR)/CS2SP.dll" "$(PLUGIN_DEST)/"
	@cp -f "$(PUBLISH_DIR)/CS2SP.Logic.dll" "$(PLUGIN_DEST)/"
	@if [ -f "$(PUBLISH_DIR)/CS2SP.deps.json" ]; then \
		cp -f "$(PUBLISH_DIR)/CS2SP.deps.json" "$(PLUGIN_DEST)/"; \
	fi
	@if [ -x ../scripts/cs2sp-select.sh ]; then \
		CS2SP_ADDONS="$(ADDONS_DIR)" ../scripts/cs2sp-select.sh css; \
	else \
		rm -f "$(ADDONS_DIR)/metamod/cs2sp.vdf"; \
		$(ECHO) -e "\e[1m CSS CS2SP deployed. Metamod cs2sp.vdf removed (C++ plugin will not load).\e[0m"; \
	fi
.PHONY: copy-addons

copy-resources: ## Install documented default convar JSON (plugin does not read it)
	@$(ECHO) -e "\e[1m Copying CS2StatsSharp resources\e[0m"
	@if [ -d "$(ADDONS_DIR)" ] && [ -d resources/addons ]; then \
		cp -rf resources/addons/* "$(ADDONS_DIR)/"; \
		$(ECHO) -e "\e[1m  addons -> $(ADDONS_DIR)\e[0m"; \
	fi
.PHONY: copy-resources

cr: ## Compile and copy CSS plugin + resources (no restart)
	$(MAKE) compile
	$(MAKE) copy-addons
	$(MAKE) copy-resources
	@$(ECHO) -e "\e[1m\n\n CR DONE \e[0m"
.PHONY: cr

dcres: ## Compile CSS plugin, stop, swap in CSS CS2SP, restart cs2mm
	$(MAKE) compile
	-$(DOCKER) stop $(CONTAINER_CS2)
	$(MAKE) copy-addons
	$(MAKE) copy-resources
	@if [ -x ../scripts/cs2sp-select.sh ]; then CS2SP_ADDONS="$(ADDONS_DIR)" ../scripts/cs2sp-select.sh css; fi
	$(DOCKER) start $(CONTAINER_CS2)
	$(DOCKER) attach $(CONTAINER_CS2)
	echo -e "\e[1m\n\n DOCKER CRES DONE \e[0m"
.PHONY: dcres
