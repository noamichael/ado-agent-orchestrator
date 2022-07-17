#!/bin/bash

NAMESPACE=default

# Deploy Ngnix
kubectl apply -n ${NAMESPACE} -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml

# Wait for it to be ready
kubectl wait \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s

# Deploy the registry onto kubernetes
kubectl apply -n ${NAMESPACE} -f - << EOF
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: example-ingress
  annotations:
    nginx.ingress.kubernetes.io/proxy-body-size: "0"
spec:
  rules:
  - http:
      paths:
      - pathType: Prefix
        path: "/"
        backend:
          service:
            name: registry
            port:
              number: 5000
---
kind: Service
apiVersion: v1
metadata:
  name: registry
spec:
  selector:
    app: registry
  ports:
  - port: 5000
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: registry-deployment
  labels:
    app: registry
spec:
  replicas: 1
  selector:
    matchLabels:
      app: registry
  template:
    metadata:
      labels:
        app: registry
    spec:
      containers:
      - name: registry
        image: registry:2
EOF

# Wait for the registry to be ready
kubectl wait deployment/registry -n ${NAMESPACE} --for condition=Available --timeout=60s