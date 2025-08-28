pipeline {
    agent any
    
    environment {
        APP_NAME = 'email-worker'
        BUILD_NUMBER = "${env.BUILD_NUMBER}"
        GITOPS_REPO = 'git@github.com:sara-golombeck/gitops-email-service.git'
        HELM_VALUES_PATH = 'charts/email-service/values.yaml'
    }
    
    triggers {
        githubPush()
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Build') {
            steps {
                script {
                    docker.build("${APP_NAME}:${BUILD_NUMBER}")
                }
            }
        }
        
        stage('Create Version Tag') {
            when { 
                branch 'main' 
            }
            steps {
                script {
                    echo "Creating version tag..."
                    
                    sshagent(credentials: ['github-ssh-key']) {
                        sh "git fetch --tags"
                        
                        def newTag = "1.0.0"  // default
                        
                        try {
                            def lastTag = sh(script: "git tag --sort=-version:refname | head -1", returnStdout: true).trim()
                            if (lastTag && lastTag != '') {
                                echo "Found existing tag: ${lastTag}"
                                
                                def v = lastTag.tokenize('.')
                                if (v.size() >= 3) {
                                    def newPatch = v[2].toInteger() + 1
                                    newTag = v[0] + "." + v[1] + "." + newPatch
                                }
                            } else {
                                echo "No existing tags found, starting from 1.0.0"
                            }
                        } catch (Exception e) {
                            echo "Error reading tags: ${e.getMessage()}, starting from 1.0.0"
                        }
                        
                        echo "Generated new tag: ${newTag}"
                        env.WORKER_TAG = newTag
                        echo "Version tag ${env.WORKER_TAG} prepared successfully"
                    }
                }
            }
        }
        
        stage('Push to ECR') {
            when { 
                branch 'main'
            }
            steps {
                script {
                    if (!env.WORKER_TAG || env.WORKER_TAG == '' || env.WORKER_TAG == 'null') {
                        error("env.WORKER_TAG is empty, null, or invalid: '${env.WORKER_TAG}'")
                    }
                    
                    echo "Pushing ${env.WORKER_TAG} to ECR..."
                    
                    withCredentials([
                        string(credentialsId: 'aws-account-id', variable: 'AWS_ACCOUNT_ID'),
                        string(credentialsId: 'aws-region', variable: 'AWS_REGION')
                    ]) {
                        def ECR_REPO_WORKER = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-worker"
                        
                        sh '''
                            aws ecr get-login-password --region "${AWS_REGION}" | \
                                docker login --username AWS --password-stdin "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
                            
                            docker tag "${APP_NAME}:${BUILD_NUMBER}" "''' + ECR_REPO_WORKER + ''':${WORKER_TAG}"
                            docker tag "${APP_NAME}:${BUILD_NUMBER}" "''' + ECR_REPO_WORKER + ''':latest"
                            
                            docker push "''' + ECR_REPO_WORKER + ''':${WORKER_TAG}"
                            docker push "''' + ECR_REPO_WORKER + ''':latest"
                        '''
                    }
                    
                    echo "Successfully pushed ${env.WORKER_TAG} to ECR"
                }
            }
        }
        
        stage('Deploy via GitOps') {
            when { 
                branch 'main' 
            }
            steps {
                script {
                    if (!env.WORKER_TAG || env.WORKER_TAG == '') {
                        echo "WARNING: env.WORKER_TAG not set, skipping GitOps update"
                        return
                    }
                    
                    sshagent(['github-ssh-key']) {
                        sh '''
                            rm -rf gitops-config
                            echo "Cloning GitOps repository..."
                            git clone "${GITOPS_REPO}" gitops-config
                        '''
                        
                        withCredentials([
                            string(credentialsId: 'git-username', variable: 'GIT_USERNAME'),
                            string(credentialsId: 'git-email', variable: 'GIT_EMAIL')
                        ]) {
                            dir('gitops-config') {
                                sh '''
                                    git config user.email "${GIT_EMAIL}"
                                    git config user.name "${GIT_USERNAME}"

                                    sed -i '/^  images:/,/^[^ ]/ s/worker: ".*"/worker: "'${WORKER_TAG}'"/' "${HELM_VALUES_PATH}"
                                    
                                    if git diff --quiet "${HELM_VALUES_PATH}"; then
                                        echo "No changes to deploy - version ${WORKER_TAG} already deployed"
                                    else
                                        git add "${HELM_VALUES_PATH}"
                                        git commit -m "Deploy worker v${WORKER_TAG} - Build ${BUILD_NUMBER}"
                                        git push origin main
                                        echo "GitOps updated: ${WORKER_TAG}"
                                    fi
                                '''
                            }
                        }
                    }
                }
            }
        }
        
        stage('Push Git Tag') {
            when { 
                branch 'main'
            }
            steps {
                script {
                    if (!env.WORKER_TAG || env.WORKER_TAG == '') {
                        echo "WARNING: env.WORKER_TAG not set, skipping git tag"
                        return
                    }
                    
                    echo "Pushing tag ${env.WORKER_TAG} to repository..."
                    
                    sshagent(credentials: ['github-ssh-key']) {
                        withCredentials([
                            string(credentialsId: 'git-username', variable: 'GIT_USERNAME'),
                            string(credentialsId: 'git-email', variable: 'GIT_EMAIL')
                        ]) {
                            sh '''
                                git config user.email "${GIT_EMAIL}"
                                git config user.name "${GIT_USERNAME}"
                                
                                git tag -a "${WORKER_TAG}" -m "Release ${WORKER_TAG} - Build ${BUILD_NUMBER}"
                                git push origin "${WORKER_TAG}"
                            '''
                        }
                    }
                    
                    echo "Tag ${env.WORKER_TAG} pushed successfully"
                }
            }
        }
    }
    
    post {
        always {
            sh '''
                rm -rf gitops-config || true
                docker rmi "${APP_NAME}:${BUILD_NUMBER}" || true
                docker image prune -f || true
            '''
            cleanWs()
        }
        success {
            echo 'Worker pipeline completed successfully!'
        }
        failure {
            echo 'Worker pipeline failed!'
        }
    }
}