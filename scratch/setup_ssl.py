import paramiko
import time
import sys

VPS_IP = "103.173.155.234"
VPS_USER = "root"
VPS_PASS = "3VctnF28"
DOMAIN = "schedule-manager.duckdns.org"
EMAIL = "tnwan007@gmail.com"

def safe_print(text, end="\n"):
    if not text:
        return
    try:
        print(text, end=end)
    except UnicodeEncodeError:
        safe_text = text.encode(sys.stdout.encoding or 'utf-8', errors='replace').decode(sys.stdout.encoding or 'utf-8')
        print(safe_text, end=end)

def run_remote_command(ssh_client, command):
    print(f"\n[REMOTE] Running: {command}")
    stdin, stdout, stderr = ssh_client.exec_command(command)
    
    output_lines = []
    while True:
        line = stdout.readline()
        if not line:
            break
        output_lines.append(line)
        safe_print(line, end="")
            
    err = stderr.read().decode('utf-8')
    exit_status = stdout.channel.recv_exit_status()
    
    if exit_status != 0:
        print(f"\n[REMOTE ERROR] Exit code: {exit_status}")
        safe_print(f"Stderr: {err}")
        raise Exception(f"Remote command failed: {command}")
    
    return "".join(output_lines)

def main():
    print("=== STARTING SSL SETUP FOR VPS ===")
    
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    print(f"Connecting to VPS at {VPS_IP}...")
    ssh.connect(VPS_IP, username=VPS_USER, password=VPS_PASS, timeout=30)
    print("Connected successfully!")
    
    # 1. Allow port 443 in UFW firewall
    print("\n--- 1. Configuring Firewall (Allow Port 443) ---")
    run_remote_command(ssh, "ufw allow 443/tcp")
    run_remote_command(ssh, "ufw reload")
    
    # 2. Update Nginx Config with sslip.io server name
    print("\n--- 2. Updating Nginx Configuration ---")
    nginx_config = f"""server {{
    listen 80;
    server_name {DOMAIN} {VPS_IP};

    location / {{
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }}
}}
"""
    sftp = ssh.open_sftp()
    temp_nginx_path = "/tmp/nginx-schedule-ssl"
    with sftp.file(temp_nginx_path, "w") as f:
        f.write(nginx_config)
    sftp.close()
    
    run_remote_command(ssh, f"mv {temp_nginx_path} /etc/nginx/sites-available/schedule")
    run_remote_command(ssh, "nginx -t")
    run_remote_command(ssh, "systemctl restart nginx")
    
    # 3. Install Certbot
    print("\n--- 3. Installing Certbot & Nginx plugin ---")
    run_remote_command(ssh, "apt-get update")
    run_remote_command(ssh, "DEBIAN_FRONTEND=noninteractive apt-get install -y certbot python3-certbot-nginx")
    
    # 4. Generate & Configure SSL Certificate
    print("\n--- 4. Running Certbot to secure domain ---")
    certbot_cmd = f"certbot --nginx --non-interactive --agree-tos --email {EMAIL} -d {DOMAIN}"
    try:
        run_remote_command(ssh, certbot_cmd)
        print("\n=== SSL CERTIFICATE CONFIGURED SUCCESSFULLY! ===")
        print(f"You can now access your app securely at: https://{DOMAIN}")
    except Exception as e:
        print(f"\n[ERROR] Certbot execution failed: {e}")
        print("Please check Nginx config or DNS resolution and try again.")
        
    ssh.close()

if __name__ == "__main__":
    main()
