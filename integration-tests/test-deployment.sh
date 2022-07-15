#!/bin/bash

# This script will deploy a local kind cluster
# and test actually spinning up the ado-agent-orchestrator
# onto a k8s cluster. Afterwards, it will spin up a job
# on ADO and ensure it correct provisions the agent

# Exit 1 on ANY error
set -o pipefail

# The ado-agent-orchestrator to test
IMAGE_TO_TEST=$1
# The Azue DevOps Org URL
ORG_URL=$2
# The Azure DevOps Personal Access Token
ORG_PAT=$3
# The project to trigger the build in
PROJECT=$4
# The name of the pipeline to trigger
PIPELINE_NAME=$5
# The agent image to run - TODO: Convert into a parameter
JOB_IMAGE=ghcr.io/akanieski/ado-pipelines-linux:0.0.1-preview
# The agent pool(s) to pool - TODO: Convert into a parameter
AGENT_POOLS=test-agent-pool
# The timeout for the test
TEST_TIMEOUT=30s
# The namespace of the jobs
NAMESPACE=default

function log() {
    TIMESTAMP=$(date +"%Y-%m-%dT%H:%M:%S")
    LEVEL=${2:-INFO}
    echo "[${LEVEL}][${TIMESTAMP}] ${1}"
}

log "-- Starting integration test ---" 
log "Deploying orchestrator - will wait ${TEST_TIMEOUT} for it to be ready..."

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
        - name: MINIMUM_AGENT_COUNT
          value: "1"
EOF

# Wait for the deployment to become ready
kubectl wait deployment/ado-orchestrator-deployment -n ${NAMESPACE} --for condition=Available --timeout=${TEST_TIMEOUT}

log "Orchestrator successfully deployed"

log "Logging into Azure"

echo  ${ORG_PAT} | az devops login --organization ${ORG_URL}

log "Asserting there are no jobs"

JOB_COUNT=$(kubectl get job -n ${NAMESPACE} --no-headers | wc -l)

if [ "${JOB_COUNT}" -gt 0 ]; then
    log "Assertion failed: expected 0 jobs, got ${JOB_COUNT}" "ERROR"
    exit 1
fi

log "Triggering Pipeline ${PIPELINE_NAME}"

az pipelines run --name ${PIPELINE_NAME}  --organization ${ORG_URL} --project ${PROJECT}

# try up to 10 times for the job to be created
for i in {1..10}
do
   JOB_COUNT=$(kubectl get job -n ${NAMESPACE} --no-headers | wc -l)
   if [ "${JOB_COUNT}" -ne 1 ]; then
    break
   fi
   sleep 5s
done

# Check one more time in case above loop ran 10 times without starting job
JOB_COUNT=$(kubectl get job -n ${NAMESPACE} --no-headers | wc -l)

if [ "${JOB_COUNT}" -ne 1 ]; then
    log "Assertion failed: expected 1 jobs, got ${JOB_COUNT}" "ERROR"
    exit 1
fi

JOB_NAME=$(kubectl get job -n ${NAMESPACE} -o=jsonpath="{.items[0].metadata.labels.job-name}")

log "Waiting ${TEST_TIMEOUT} for Job/${JOB_NAME} to finish"

# Wait for job to finish
kubectl wait job/${JOB_NAME} -n ${NAMESPACE} --for condition=Complete --timeout=${TEST_TIMEOUT}

log "-- Result: SUCCESS ---" 