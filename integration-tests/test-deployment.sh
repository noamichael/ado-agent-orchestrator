#!/bin/bash

# This script will deploy a local kind cluster
# and test actually spinning up the ado-agent-orchestrator
# onto a k8s cluster. Afterwards, it will spin up a job
# on ADO and ensure it correct provisions the agent

# The ado-agent-orchestrator to test
IMAGE_TO_TEST=$0
# The Azue DevOps Org URL
ORG_URL=$1
# The Azure DevOps Personal Access Token
ORG_PAT=$2
# The agent image to run - TODO: Convert into a parameter
JOB_IMAGE=ghcr.io/akanieski/ado-pipelines-linux:0.0.1-preview
# The agent pool(s) to pool - TODO: Convert into a parameter
AGENT_POOLS=test-agent-pool

# Load the newly built image into kind
kind load docker-image $IMAGE_TO_TEST
# Load the agent we will test
kind load docker-image $JOB_IMAGE

# Deploy the agent-orchestrator onto kubernetes
kubectl apply -f - << EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ado-orchestrator-deployment
  labels:
    app: ado-orchestrator
spec:
  replicas: 1
  selector:
    matchLabels:
      app: ado-orchestrator
  template:
    metadata:
      labels:
        app: ado-orchestrator
    spec:
      containers:
      - name: ado-orchestrator
        image: ${IMAGE_TO_TEST}
        imagePullPolicy: Never
        env:
        - name: ORG_URL
          value: "${ORG_URL}"
        - name: ORG_PAT
          value: "${ORG_PAT}"
        - name: AGENT_POOLS
          value: "${AGENT_POOLS}"
        - name: JOB_IMAGE
          value: "${JOB_IMAGE}"
        - name: JOB_NAMESPACE
          value: "default"
EOF

# TODO: Trigger an ADO pipeline somehow