docker pull wnameless/oracle-xe-11g
docker run -d -p 49160:22 -p 1521:1521 -e ORACLE_ALLOW_REMOTE=true wnameless/oracle-xe-11g