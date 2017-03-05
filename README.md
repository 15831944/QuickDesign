# QuickDesign



C# NX 二次开发 IStorage

1、创建本地分支 local_branch

     Git branch local_branch


2、创建本地分支local_branch 并切换到local_branch分支

   git checkout -b local_branch


3、切换到分支local_branch

    git checkout local_branch


4、推送本地分支local_branch到远程分支 remote_branch并建立关联关系

      a.远程已有remote_branch分支并且已经关联本地分支local_branch且本地已经切换到local_branch

          git push

     b.远程已有remote_branch分支但未关联本地分支local_branch且本地已经切换到local_branch

         git push -u origin/remote_branch

     c.远程没有有remote_branch分支并，本地已经切换到local_branch

        git push origin local_branch:remote_branch

5、删除本地分支local_branch

      git branch -d local_branch

6、删除远程分支remote_branch

     git push origin  :remote_branch

     git branch -m | -M oldbranch newbranch 重命名分支，如果newbranch名字分支已经存在，则需要使用-M强制重命名，否则，使用-m进行重命名。

   git branch -d | -D branchname 删除branchname分支

   git branch -d -r branchname 删除远程branchname分支


7、查看本地分支

      git branch


8、查看远程和本地分支

      git branch -a